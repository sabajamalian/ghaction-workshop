#!/usr/bin/env bash
# Sets up Azure for the workshop site that .github/workflows/deploy-site.yml deploys. Run it with --help to see the options.
set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
# shellcheck source-path=SCRIPTDIR source=common.sh
. "$SCRIPT_DIR/common.sh"

usage() {
    cat <<EOF
Usage: scripts/azure/site-setup.sh --app-name NAME [options]

Sets up Azure for the workshop site, which .github/workflows/deploy-site.yml deploys. It creates a
resource group, a Linux App Service plan, a web app with the .NET 10 runtime, and a user-assigned
managed identity. The identity gets a federated credential that trusts GitHub Actions jobs that use
the repository's production environment, and the Website Contributor role on the web app, and
nothing else. At the end, the script prints the four repository variables the workflow uses.

Required:
  --app-name NAME          Name of the web app. The site's address is NAME.azurewebsites.net,
                           so the name must be unique across Azure: 2 to 55 lowercase letters,
                           digits, and hyphens.

Options:
  --repo OWNER/REPO        Repository that deploys the site. Default: sabajamalian/ghaction-workshop
  --environment NAME       Deployment environment the workflow's job uses. Default: production
  --resource-group NAME    Resource group to use or create. Default: rg-NAME, from --app-name
  --location REGION        Region for new resources. Default: the resource group's region if
                           the group exists, otherwise $DEFAULT_LOCATION
  --sku F1|B1              Pricing tier for a new App Service plan. Default: B1
  --subject-prefix PREFIX  Start of the OIDC subject, such as repo:OWNER@ID/REPO@ID. Default:
                           read from GitHub
  --dry-run                Print the commands that would change something instead of running
                           them. Read-only checks still run.
  -h, --help               Show this help.

Other names: App Service plan plan-NAME, identity id-NAME, and federated credential
github-env-ENVIRONMENT.

You need Owner, or Contributor plus User Access Administrator or Role Based Access Control
Administrator, on the subscription or on the resource group. See scripts/azure/README.md.
EOF
}

APP_NAME=""
REPO="sabajamalian/ghaction-workshop"
ENVIRONMENT="production"
RESOURCE_GROUP=""
LOCATION=""
SKU="B1"
SKU_GIVEN=false
GIVEN_PREFIX=""

while [ $# -gt 0 ]; do
    case $1 in
        --app-name) require_value "$@"; APP_NAME=$2; shift 2 ;;
        --repo) require_value "$@"; REPO=$2; shift 2 ;;
        --environment) require_value "$@"; ENVIRONMENT=$2; shift 2 ;;
        --resource-group) require_value "$@"; RESOURCE_GROUP=$2; shift 2 ;;
        --location) require_value "$@"; LOCATION=$2; shift 2 ;;
        --sku) require_value "$@"; SKU=$2; SKU_GIVEN=true; shift 2 ;;
        --subject-prefix) require_value "$@"; GIVEN_PREFIX=$2; shift 2 ;;
        --dry-run) DRY_RUN=true; shift ;;
        -h | --help) usage; exit 0 ;;
        *) unknown_option "$1" ;;
    esac
done

if [ -z "$APP_NAME" ]; then
    die "--app-name is required. Run $0 --help to see the options."
fi
validate_app_name "$APP_NAME"
validate_repo "$REPO"
validate_environment "$ENVIRONMENT"
RESOURCE_GROUP=${RESOURCE_GROUP:-rg-$APP_NAME}
validate_resource_group "$RESOURCE_GROUP"
SKU=$(normalize_sku "$SKU")
if [ -n "$LOCATION" ]; then
    LOCATION=$(normalize_location "$LOCATION")
fi

info "Workshop site Azure setup for $REPO"
if [ "$DRY_RUN" = true ]; then
    info "Dry run: read-only checks run, and commands that would change something are printed instead."
fi
require_az
resolve_subject_prefix "$REPO" "$GIVEN_PREFIX"

CRED_NAMES=("$(credential_name env "$ENVIRONMENT")")
CRED_SUBJECTS=("$SUBJECT_PREFIX:environment:$ENVIRONMENT")

run_app_setup
