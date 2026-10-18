#!/usr/bin/env bash
# Sets up Azure for Lab 7 in your own subscription. Run it with --help to see the options.
set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
# shellcheck source-path=SCRIPTDIR source=common.sh
. "$SCRIPT_DIR/common.sh"

usage() {
    cat <<EOF
Usage: scripts/azure/learner-setup.sh --app-name NAME --repo OWNER/REPO [options]

Sets up Azure for Lab 7 in your own subscription. It creates a resource group, a Linux App Service
plan, a web app with the .NET 10 runtime, and a user-assigned managed identity. The identity gets
federated credentials that trust GitHub Actions jobs from your repository's main branch, and from
each environment you add with --environment. It also gets the Website Contributor role on the web
app, and nothing else. At the end, the script prints the four repository variables Lab 7 uses.

Required:
  --app-name NAME          Name of the web app. The site's address is NAME.azurewebsites.net,
                           so the name must be unique across Azure: 2 to 55 lowercase letters,
                           digits, and hyphens.
  --repo OWNER/REPO        Your copy of the workshop repository.

Options:
  --branch NAME            Branch the workflow deploys from. Default: main
  --environment NAME       Also trust jobs that use this deployment environment, such as staging
                           for Lab 8. Repeat it to add more environments.
  --resource-group NAME    Resource group to use or create. Default: rg-NAME, from --app-name
  --location REGION        Region for new resources. Default: the resource group's region if
                           the group exists, otherwise $DEFAULT_LOCATION
  --sku F1|B1              Pricing tier for a new App Service plan. Default: F1, which is free
  --subject-prefix PREFIX  Start of the OIDC subject, such as repo:OWNER@ID/REPO@ID. Default:
                           read from GitHub
  --dry-run                Print the commands that would change something instead of running
                           them. Read-only checks still run.
  -h, --help               Show this help.

Other names: App Service plan plan-NAME, identity id-NAME, and federated credentials
github-branch-BRANCH and github-env-ENVIRONMENT.

You need Owner, or Contributor plus User Access Administrator or Role Based Access Control
Administrator, on the subscription or on the resource group. See scripts/azure/README.md.
EOF
}

APP_NAME=""
REPO=""
BRANCH="main"
ENVIRONMENTS=()
RESOURCE_GROUP=""
LOCATION=""
SKU="F1"
SKU_GIVEN=false
GIVEN_PREFIX=""

add_environment() {
    local existing
    validate_environment "$1"
    for existing in ${ENVIRONMENTS[@]+"${ENVIRONMENTS[@]}"}; do
        if [ "$existing" = "$1" ]; then
            return 0
        fi
    done
    ENVIRONMENTS+=("$1")
}

while [ $# -gt 0 ]; do
    case $1 in
        --app-name) require_value "$@"; APP_NAME=$2; shift 2 ;;
        --repo) require_value "$@"; REPO=$2; shift 2 ;;
        --branch) require_value "$@"; BRANCH=$2; shift 2 ;;
        --environment) require_value "$@"; add_environment "$2"; shift 2 ;;
        --resource-group) require_value "$@"; RESOURCE_GROUP=$2; shift 2 ;;
        --location) require_value "$@"; LOCATION=$2; shift 2 ;;
        --sku) require_value "$@"; SKU=$2; SKU_GIVEN=true; shift 2 ;;
        --subject-prefix) require_value "$@"; GIVEN_PREFIX=$2; shift 2 ;;
        --dry-run) DRY_RUN=true; shift ;;
        -h | --help) usage; exit 0 ;;
        *) unknown_option "$1" ;;
    esac
done

if [ -z "$APP_NAME" ] && [ -z "$REPO" ]; then
    die "--app-name and --repo are required. Run $0 --help to see the options."
elif [ -z "$APP_NAME" ]; then
    die "--app-name is required. Run $0 --help to see the options."
elif [ -z "$REPO" ]; then
    die "--repo is required. Run $0 --help to see the options."
fi
validate_app_name "$APP_NAME"
validate_repo "$REPO"
validate_branch "$BRANCH"
RESOURCE_GROUP=${RESOURCE_GROUP:-rg-$APP_NAME}
validate_resource_group "$RESOURCE_GROUP"
SKU=$(normalize_sku "$SKU")
if [ -n "$LOCATION" ]; then
    LOCATION=$(normalize_location "$LOCATION")
fi

info "Lab 7 Azure setup for $REPO"
if [ "$DRY_RUN" = true ]; then
    info "Dry run: read-only checks run, and commands that would change something are printed instead."
fi
require_az
resolve_subject_prefix "$REPO" "$GIVEN_PREFIX"

CRED_NAMES=("$(credential_name branch "$BRANCH")")
CRED_SUBJECTS=("$SUBJECT_PREFIX:ref:refs/heads/$BRANCH")
for environment in ${ENVIRONMENTS[@]+"${ENVIRONMENTS[@]}"}; do
    CRED_NAMES+=("$(credential_name env "$environment")")
    CRED_SUBJECTS+=("$SUBJECT_PREFIX:environment:$environment")
done

run_app_setup
