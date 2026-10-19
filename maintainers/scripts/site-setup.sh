#!/usr/bin/env bash
# Creates the Azure resources for the workshop site, which deploy-site.sh deploys. Run it with --help to see the options.
set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
# shellcheck source-path=SCRIPTDIR source=../../scripts/azure/common.sh
. "$SCRIPT_DIR/../../scripts/azure/common.sh"

usage() {
    cat <<EOF
Usage: maintainers/scripts/site-setup.sh --app-name NAME [options]

Creates the Azure resources for the workshop site: a resource group, a Linux App Service plan, and
a web app with the .NET 10 runtime, HTTPS only, and basic authentication off. It creates no
identity and no credentials, because maintainers/scripts/deploy-site.sh deploys with your own
Azure CLI sign-in.

Required:
  --app-name NAME          Name of the web app. The site's address is NAME.azurewebsites.net,
                           so the name must be unique across Azure: 2 to 55 lowercase letters,
                           digits, and hyphens.

Options:
  --resource-group NAME    Resource group to use or create. Default: rg-NAME, from --app-name
  --location REGION        Region for new resources. Default: the resource group's region if
                           the group exists, otherwise $DEFAULT_LOCATION
  --sku F1|B1              Pricing tier for a new App Service plan. Default: B1
  --dry-run                Print the commands that would change something instead of running
                           them. Read-only checks still run.
  -h, --help               Show this help.

The App Service plan is named plan-NAME. You need Contributor on the subscription, or on an
existing resource group. See maintainers/deployment.md.
EOF
}

APP_NAME=""
RESOURCE_GROUP=""
LOCATION=""
SKU="B1"
SKU_GIVEN=false

while [ $# -gt 0 ]; do
    case $1 in
        --app-name) require_value "$@"; APP_NAME=$2; shift 2 ;;
        --resource-group) require_value "$@"; RESOURCE_GROUP=$2; shift 2 ;;
        --location) require_value "$@"; LOCATION=$2; shift 2 ;;
        --sku) require_value "$@"; SKU=$2; SKU_GIVEN=true; shift 2 ;;
        --dry-run) DRY_RUN=true; shift ;;
        -h | --help) usage; exit 0 ;;
        *) unknown_option "$1" ;;
    esac
done

if [ -z "$APP_NAME" ]; then
    die "--app-name is required. Run $0 --help to see the options."
fi
validate_app_name "$APP_NAME"
RESOURCE_GROUP=${RESOURCE_GROUP:-rg-$APP_NAME}
validate_resource_group "$RESOURCE_GROUP"
SKU=$(normalize_sku "$SKU")
if [ -n "$LOCATION" ]; then
    LOCATION=$(normalize_location "$LOCATION")
fi

info "Workshop site Azure setup"
if [ "$DRY_RUN" = true ]; then
    info "Dry run: read-only checks run, and commands that would change something are printed instead."
fi
require_az

PLAN="plan-$APP_NAME"
inspect_group "$RESOURCE_GROUP"
if [ -z "$LOCATION" ]; then
    LOCATION=${GROUP_LOCATION:-$DEFAULT_LOCATION}
fi
inspect_webapp "$RESOURCE_GROUP" "$APP_NAME"
require_usable_app_name
ensure_providers Microsoft.Web

section "Resources:"
ensure_group "$RESOURCE_GROUP" "$LOCATION"
if [ "$WEBAPP_EXISTS" != true ]; then
    ensure_plan "$RESOURCE_GROUP" "$PLAN" "$LOCATION" "$SKU" "$SKU_GIVEN"
fi
ensure_webapp "$RESOURCE_GROUP" "$APP_NAME" "$PLAN"

section "Site: https://$WEBAPP_HOST"
section "To deploy the site from this repository:"
info "  bash maintainers/scripts/deploy-site.sh --app-name $APP_NAME --resource-group $RESOURCE_GROUP"

section "To delete everything this script created:"
if [ "$GROUP_TAGGED" = true ] || [ "$GROUP_CREATED" = true ]; then
    print_cmd group delete --name "$RESOURCE_GROUP"
else
    print_cmd webapp delete --name "$APP_NAME" --resource-group "$RESOURCE_GROUP"
    print_cmd appservice plan delete --name "$PLAN" --resource-group "$RESOURCE_GROUP"
fi
finish
