#!/usr/bin/env bash
# Tests, publishes, and deploys the workshop site from this checkout. Run it with --help to see the options.
set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
REPO_ROOT=$(cd "$SCRIPT_DIR/../.." && pwd)
# shellcheck source-path=SCRIPTDIR source=../../scripts/azure/common.sh
. "$REPO_ROOT/scripts/azure/common.sh"

usage() {
    cat <<EOF
Usage: maintainers/scripts/deploy-site.sh --app-name NAME [options]

Deploys the workshop site from the commit you have checked out. It runs every test, publishes the
dashboard, uploads it to the web app with az webapp deploy, and then waits until the site's
/health page reports that commit. It signs in to Azure with your Azure CLI session.

Required:
  --app-name NAME          Name of the web app that site-setup.sh created.

Options:
  --resource-group NAME    The web app's resource group. Default: rg-NAME, from --app-name
  --repo OWNER/REPO        Repository the site's footer links to.
                           Default: sabajamalian/ghaction-workshop
  --nuget-config FILE      NuGet.Config file for dotnet restore, if your network needs one.
  --allow-changes          Deploy even when the checkout has uncommitted changes. The site
                           then reports a commit that doesn't match what it runs.
  -h, --help               Show this help.

You need Website Contributor, or Contributor, on the web app. See maintainers/deployment.md.
EOF
}

APP_NAME=""
RESOURCE_GROUP=""
REPO="sabajamalian/ghaction-workshop"
NUGET_CONFIG=""
ALLOW_CHANGES=false

while [ $# -gt 0 ]; do
    case $1 in
        --app-name) require_value "$@"; APP_NAME=$2; shift 2 ;;
        --resource-group) require_value "$@"; RESOURCE_GROUP=$2; shift 2 ;;
        --repo) require_value "$@"; REPO=$2; shift 2 ;;
        --nuget-config) require_value "$@"; NUGET_CONFIG=$2; shift 2 ;;
        --allow-changes) ALLOW_CHANGES=true; shift ;;
        -h | --help) usage; exit 0 ;;
        *) unknown_option "$1" ;;
    esac
done

if [ -z "$APP_NAME" ]; then
    die "--app-name is required. Run $0 --help to see the options."
fi
validate_app_name "$APP_NAME"
validate_repo "$REPO"
RESOURCE_GROUP=${RESOURCE_GROUP:-rg-$APP_NAME}
validate_resource_group "$RESOURCE_GROUP"

for tool in git dotnet zip curl jq; do
    if ! command -v "$tool" >/dev/null 2>&1; then
        die "$tool isn't installed. Install it, or run this script in Azure Cloud Shell."
    fi
done

cd "$REPO_ROOT"
if [ -n "$(git status --porcelain)" ]; then
    if [ "$ALLOW_CHANGES" = true ]; then
        warn "The checkout has uncommitted changes. They're deployed, but the site reports the last commit."
    else
        die "The checkout has uncommitted changes. Commit or stash them, or pass --allow-changes."
    fi
fi
COMMIT=$(git rev-parse HEAD)
info "Deploying commit $COMMIT ($(git rev-parse --abbrev-ref HEAD))."

require_az
SITE_HOST=$(query_az webapp show --name "$APP_NAME" --resource-group "$RESOURCE_GROUP" --query defaultHostName --output tsv 2>/dev/null) \
    || die "Couldn't find web app $APP_NAME in resource group $RESOURCE_GROUP. Run maintainers/scripts/site-setup.sh first, or pass --resource-group."
SITE_URL="https://$SITE_HOST"

WORK_DIR=$(mktemp -d)
trap 'rm -rf "$WORK_DIR"' EXIT

section "Restoring, testing, and publishing:"
restore_args=(restore GitHubActionsWorkshop.slnx)
if [ -n "$NUGET_CONFIG" ]; then
    restore_args+=(--configfile "$NUGET_CONFIG")
fi
dotnet "${restore_args[@]}"
dotnet test GitHubActionsWorkshop.slnx --configuration Release --no-restore
# The dashboard's footer and /health read these properties. A GitHub Actions run sets them from its
# environment variables, so this build passes the same values.
dotnet publish src/WorkshopDashboard/WorkshopDashboard.csproj --configuration Release --no-restore --output "$WORK_DIR/publish" \
    -p:GITHUB_ACTIONS=true \
    -p:GITHUB_SHA="$COMMIT" \
    -p:GITHUB_SERVER_URL=https://github.com \
    -p:GITHUB_REPOSITORY="$REPO"
(cd "$WORK_DIR/publish" && zip -q -r "$WORK_DIR/site.zip" .)

section "Deploying to $APP_NAME:"
print_cmd webapp deploy --name "$APP_NAME" --resource-group "$RESOURCE_GROUP" --src-path "$WORK_DIR/site.zip" --type zip
if ! az webapp deploy --name "$APP_NAME" --resource-group "$RESOURCE_GROUP" --src-path "$WORK_DIR/site.zip" --type zip --only-show-errors >/dev/null; then
    die "The deployment failed. Fix the problem the Azure CLI reported, then run the script again."
fi

section "Checking that $SITE_URL runs $COMMIT:"
for attempt in 1 2 3 4 5 6 7 8 9 10; do
    commit=$(curl --silent --location --max-time 30 "$SITE_URL/health" | jq -r '.commit' 2>/dev/null || true)
    if [ "$commit" = "$COMMIT" ]; then
        info "$SITE_URL is running commit $commit."
        exit 0
    fi
    info "Attempt $attempt of 10: /health reported commit '${commit:-nothing}'. Trying again in 15 seconds."
    sleep 15
done
die "$SITE_URL/health didn't report commit $COMMIT after 10 attempts. Check the log stream: az webapp log tail --name $APP_NAME --resource-group $RESOURCE_GROUP"
