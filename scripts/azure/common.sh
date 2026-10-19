#!/usr/bin/env bash
# shellcheck disable=SC2034,SC2153
# Settings and helpers that learner-setup.sh and instructor-setup.sh share. The maintainer's scripts in
# maintainers/scripts use them too. Those scripts source this file. It does nothing on its own.

if [ "${BASH_SOURCE[0]}" = "$0" ]; then
    echo "common.sh only holds shared helpers. Run learner-setup.sh or instructor-setup.sh instead." >&2
    exit 1
fi

OIDC_ISSUER="https://token.actions.githubusercontent.com"
OIDC_AUDIENCE="api://AzureADTokenExchange"
DEPLOY_ROLE="Website Contributor"
WEBAPP_RUNTIME="DOTNETCORE:10.0"
WORKSHOP_TAG="created-by=ghaction-workshop"
DEFAULT_LOCATION="eastus"
NAME_CHECK_API_VERSION="2024-11-01"

DRY_RUN=false
SUBSCRIPTION_ID=""
TENANT_ID=""
SUBSCRIPTION_NAME=""
SUBJECT_PREFIX=""
PREFIX_ERROR=""
PREFIX_HINT="pass the prefix with --subject-prefix (scripts/azure/README.md shows how to find it)"
GROUP_EXISTS=false
GROUP_CREATED=false
GROUP_TAGGED=false
GROUP_LOCATION=""
WEBAPP_EXISTS=false
WEBAPP_ID=""
WEBAPP_HOST=""
IDENTITY_EXISTS=false
IDENTITY_CLIENT_ID=""
IDENTITY_PRINCIPAL_ID=""
NAME_STATUS=""
NAME_REASON=""
NAME_GROUP=""

# Output

info() {
    printf '%s\n' "$*"
}

section() {
    printf '\n%s\n' "$*"
}

warn() {
    printf 'Warning: %s\n' "$*" >&2
}

die() {
    printf 'Error: %s\n' "$*" >&2
    exit 1
}

# Describes a change before it happens: "Creating thing." or, in a dry run, "Would create thing."
doing() {
    if [ "$DRY_RUN" = true ]; then
        info "Would $2 $3."
    else
        info "$1 $3."
    fi
}

# Quotes one argument so the printed command can be copied and run.
quote_arg() {
    case $1 in
        '<'*'>') printf '%s' "$1" ;;
        '' | *[!A-Za-z0-9_./:=@,+%-]*) printf "'%s'" "$(printf '%s' "$1" | sed "s/'/'\\\\''/g")" ;;
        *) printf '%s' "$1" ;;
    esac
}

print_cmd() {
    local out="  az" arg
    for arg in "$@"; do
        out="$out $(quote_arg "$arg")"
    done
    printf '%s\n' "$out"
}

# Azure CLI calls

# Prints a command that changes something, then runs it unless this is a dry run.
run_az() {
    print_cmd "$@"
    if [ "$DRY_RUN" = true ]; then
        return 0
    fi
    if ! az "$@" >/dev/null; then
        die "That command failed. Fix the problem the Azure CLI reported, then run the script again. It reuses anything it already created."
    fi
}

# Runs a read-only query and prints its output. Dry runs run these too.
query_az() {
    local output
    if ! output=$(az "$@" --only-show-errors); then
        printf 'Error: This Azure CLI query failed, so the script stopped:\n' >&2
        print_cmd "$@" >&2
        exit 1
    fi
    printf '%s' "$output" | tr -d '\r'
}

# Prints line N of TEXT.
line() {
    printf '%s\n' "$2" | sed -n "${1}p"
}

to_lower() {
    printf '%s' "$1" | tr '[:upper:]' '[:lower:]'
}

require_value() {
    if [ $# -lt 2 ] || [ -z "$2" ] || [ "${2#--}" != "$2" ]; then
        die "$1 needs a value. Run $0 --help to see the options."
    fi
}

unknown_option() {
    case $1 in
        --*=*) die "Put a space between ${1%%=*} and its value instead of =. Run $0 --help to see the options." ;;
        *) die "Unknown option $1. Run $0 --help to see the options." ;;
    esac
}

# Validation

validate_app_name() {
    local re='^[a-z0-9]([a-z0-9-]*[a-z0-9])?$'
    if ! [[ $1 =~ $re ]] || [ ${#1} -lt 2 ] || [ ${#1} -gt 55 ]; then
        die "\"$1\" can't be a web app name. Use 2 to 55 lowercase letters, digits, and hyphens, starting and ending with a letter or digit."
    fi
}

validate_repo() {
    local re='^[A-Za-z0-9-]+/[A-Za-z0-9._-]+$'
    if ! [[ $1 =~ $re ]]; then
        die "\"$1\" isn't a repository. Use OWNER/REPO, such as octocat/ghaction-workshop."
    fi
}

validate_environment() {
    local re='^[A-Za-z0-9._-]+$'
    if ! [[ $1 =~ $re ]]; then
        die "\"$1\" can't be used as an environment name here. Use letters, digits, periods, underscores, and hyphens."
    fi
}

validate_branch() {
    local re='^[A-Za-z0-9._/-]+$'
    if ! [[ $1 =~ $re ]]; then
        die "\"$1\" can't be used as a branch name here. Use letters, digits, periods, underscores, hyphens, and slashes."
    fi
}

validate_resource_group() {
    local re='^[A-Za-z0-9._()-]+$'
    if ! [[ $1 =~ $re ]] || [ ${#1} -gt 90 ] || [ "${1%.}" != "$1" ]; then
        die "\"$1\" can't be a resource group name. Use up to 90 letters, digits, periods, underscores, hyphens, and parentheses, not ending in a period."
    fi
}

# Prints the region in lowercase, or stops when it isn't a short region name such as eastus.
normalize_location() {
    local location re='^[a-z0-9]+$'
    location=$(to_lower "$1")
    if ! [[ $location =~ $re ]]; then
        die "\"$1\" isn't a region name. Use a short name such as eastus. To list them, run: az account list-locations --query \"[].name\" --output tsv"
    fi
    printf '%s' "$location"
}

normalize_sku() {
    local sku
    sku=$(printf '%s' "$1" | tr '[:lower:]' '[:upper:]')
    case $sku in
        F1 | B1) printf '%s' "$sku" ;;
        *) die "--sku must be F1 or B1." ;;
    esac
}

# Turns a branch or environment name into a federated credential name, such as github-branch-main.
credential_name() {
    local name
    name="github-$1-$(printf '%s' "$2" | tr -c 'A-Za-z0-9_-' '-')"
    printf '%s' "$name" | cut -c1-120
}

# Azure sign-in

require_az() {
    local account version re='^([0-9]+)\.([0-9]+)'
    if ! command -v az >/dev/null 2>&1; then
        die "The Azure CLI (az) isn't installed. Install it from https://learn.microsoft.com/cli/azure/install-azure-cli, or run this script in Azure Cloud Shell, which includes it."
    fi
    # az webapp create gained --basic-auth in Azure CLI 2.58.0.
    version=$(az version --query '"azure-cli"' --output tsv --only-show-errors 2>/dev/null | tr -d '\r') || version=""
    if [[ $version =~ $re ]]; then
        if [ "${BASH_REMATCH[1]}" -lt 2 ] || { [ "${BASH_REMATCH[1]}" -eq 2 ] && [ "${BASH_REMATCH[2]}" -lt 58 ]; }; then
            die "These scripts need Azure CLI 2.58.0 or later, and you have $version. Run az upgrade, or use Azure Cloud Shell, which keeps the Azure CLI up to date."
        fi
    fi
    if ! account=$(az account show --query "[id, tenantId, name]" --output tsv --only-show-errors 2>/dev/null); then
        die "The Azure CLI isn't signed in. Run az login, then run this script again."
    fi
    if ! az account get-access-token --output none --only-show-errors 2>/dev/null; then
        die "The Azure CLI's sign-in has expired. Run az login, then run this script again."
    fi
    account=$(printf '%s' "$account" | tr -d '\r')
    SUBSCRIPTION_ID=$(line 1 "$account")
    TENANT_ID=$(line 2 "$account")
    SUBSCRIPTION_NAME=$(line 3 "$account")
    info "Azure subscription: $SUBSCRIPTION_NAME ($SUBSCRIPTION_ID)"
    info "To use a different one, run az account set --subscription NAME-OR-ID, then run this script again."
}

# GitHub OIDC subjects

# Prints a top-level string or Boolean value from a one-line JSON object.
json_field() {
    printf '%s' "$1" | sed -n -E "s/.*\"$2\"[[:space:]]*:[[:space:]]*\"?([^\",}]*)\"?.*/\1/p" | tr -d '[:space:]'
}

# Prints why PREFIX can't be the OIDC subject prefix for REPO, or nothing when it can.
prefix_problem() {
    local prefix=$1 repo=$2 plain re='^repo:[A-Za-z0-9-]+(@[0-9]+)?/[A-Za-z0-9._-]+(@[0-9]+)?$'
    if ! [[ $prefix =~ $re ]]; then
        printf '%s' "\"$prefix\" isn't an OIDC subject prefix. It looks like repo:OWNER@OWNER-ID/REPO@REPO-ID, or repo:OWNER/REPO for older repositories."
        return 0
    fi
    plain=$(printf '%s' "$prefix" | sed -E 's/@[0-9]+//g' | tr '[:upper:]' '[:lower:]')
    if [ "$plain" != "repo:$(to_lower "$repo")" ]; then
        printf '%s' "The subject prefix $prefix belongs to a different repository than $repo."
    fi
}

# Sets SUBJECT_PREFIX to the start of the subject in the OIDC tokens GitHub issues for REPO, or sets PREFIX_ERROR.
# Repositories created after July 15, 2026 use repo:OWNER@OWNER-ID/REPO@REPO-ID. Older ones use repo:OWNER/REPO.
lookup_subject_prefix() {
    local repo=$1 path response="" status="" flat use_default immutable prefix problem
    path="repos/$repo/actions/oidc/customization/sub"
    SUBJECT_PREFIX=""
    PREFIX_ERROR=""
    if command -v gh >/dev/null 2>&1 && gh auth status >/dev/null 2>&1; then
        if response=$(gh api "$path" 2>/dev/null); then
            status=200
        fi
    fi
    if [ "$status" != 200 ] && command -v curl >/dev/null 2>&1; then
        response=$(curl --silent --location --max-time 20 --header "Accept: application/vnd.github+json" \
            --write-out '\n%{http_code}' "https://api.github.com/$path" 2>/dev/null) || response=""
        status=$(printf '%s\n' "$response" | tail -n 1)
        response=$(printf '%s\n' "$response" | sed '$d')
    fi
    case $status in
        200) ;;
        404)
            PREFIX_ERROR="GitHub didn't find $repo. Check the name. If the repository is private, sign in with gh auth login as someone who can read it, or $PREFIX_HINT."
            return 0
            ;;
        403 | 429)
            PREFIX_ERROR="GitHub's API rate limit blocked the lookup for $repo. Sign in with gh auth login, then run the script again."
            return 0
            ;;
        *)
            PREFIX_ERROR="Couldn't get the OIDC subject settings for $repo from GitHub. Try again, or $PREFIX_HINT."
            return 0
            ;;
    esac
    flat=$(printf '%s' "$response" | tr -d '\r\n')
    use_default=$(json_field "$flat" use_default)
    immutable=$(json_field "$flat" use_immutable_subject)
    prefix=$(json_field "$flat" sub_claim_prefix)
    if [ "$use_default" = false ]; then
        PREFIX_ERROR="$repo uses a custom OIDC subject template, so its subjects don't follow the default format. See \"Custom subject claims\" in scripts/azure/README.md."
        return 0
    fi
    if [ -z "$prefix" ]; then
        if [ "$immutable" = true ] || [ -z "$use_default" ]; then
            PREFIX_ERROR="GitHub's answer for $repo didn't include the OIDC subject prefix, so $PREFIX_HINT."
            return 0
        fi
        prefix="repo:$repo"
    fi
    problem=$(prefix_problem "$prefix" "$repo")
    if [ -n "$problem" ]; then
        PREFIX_ERROR=$problem
        return 0
    fi
    SUBJECT_PREFIX=$prefix
}

# Sets SUBJECT_PREFIX for REPO from GIVEN, if there is one, or from GitHub. Stops when neither works.
resolve_subject_prefix() {
    local repo=$1 given=$2 problem
    if [ -n "$given" ]; then
        problem=$(prefix_problem "$given" "$repo")
        if [ -n "$problem" ]; then
            die "$problem"
        fi
        SUBJECT_PREFIX=$given
    else
        lookup_subject_prefix "$repo"
        if [ -n "$PREFIX_ERROR" ]; then
            die "$PREFIX_ERROR"
        fi
    fi
    info "OIDC subject prefix for $repo: $SUBJECT_PREFIX"
}

# Checks before anything is created

# Sets NAME_STATUS to available, taken, or unknown when the check can't run. For a taken name, sets NAME_REASON to
# Azure's explanation, and NAME_GROUP to the resource group when the web app is already in your subscription.
check_app_name() {
    local app=$1 result
    NAME_STATUS=unknown
    NAME_REASON=""
    NAME_GROUP=""
    result=$(az rest --method post \
        --url "/subscriptions/$SUBSCRIPTION_ID/providers/Microsoft.Web/checknameavailability?api-version=$NAME_CHECK_API_VERSION" \
        --body "{\"name\":\"$app\",\"type\":\"Microsoft.Web/sites\"}" \
        --query "[nameAvailable, message]" --output tsv --only-show-errors 2>/dev/null | tr -d '\r') || result=""
    case $(line 1 "$result") in
        true) NAME_STATUS=available ;;
        false)
            NAME_STATUS=taken
            NAME_REASON=$(line 2 "$result")
            if [ -z "$NAME_REASON" ] || [ "$NAME_REASON" = None ]; then
                NAME_REASON="The name is taken."
            fi
            NAME_GROUP=$(az webapp list --query "[?name=='$app'] | [0].resourceGroup" --output tsv --only-show-errors 2>/dev/null | tr -d '\r') || NAME_GROUP=""
            ;;
    esac
}

# Stops the script when a new web app can't use APP_NAME. Call it after inspect_webapp.
require_usable_app_name() {
    if [ "$WEBAPP_EXISTS" = true ]; then
        return 0
    fi
    check_app_name "$APP_NAME"
    if [ "$NAME_STATUS" = available ]; then
        info "The name $APP_NAME is available."
    elif [ "$NAME_STATUS" = unknown ]; then
        warn "Couldn't check whether the name $APP_NAME is available, so the script continues. If creating the web app fails because the name is taken, run the script again with another --app-name."
    elif [ -n "$NAME_GROUP" ]; then
        die "You already have a web app named $APP_NAME, in resource group $NAME_GROUP. To set it up, run the script again with --resource-group $NAME_GROUP."
    else
        die "Azure says the name $APP_NAME can't be used: $NAME_REASON Web app names are unique across all of Azure, so run the script again with another --app-name."
    fi
}

# Registers each resource provider NAMESPACE that isn't registered yet.
ensure_providers() {
    local namespace state
    for namespace in "$@"; do
        if ! state=$(az provider show --namespace "$namespace" --query registrationState --output tsv --only-show-errors 2>/dev/null); then
            warn "Couldn't check whether the $namespace resource provider is registered. If a command fails with MissingSubscriptionRegistration, ask a subscription owner to run az provider register --namespace $namespace"
            continue
        fi
        if [ "$(printf '%s' "$state" | tr -d '\r')" != "Registered" ]; then
            doing "Registering" "register" "the $namespace resource provider, which can take a few minutes"
            run_az provider register --namespace "$namespace" --wait
        fi
    done
}

# Resources. Each inspect_ function only reads. Each ensure_ function creates what's missing.

inspect_group() {
    local details
    GROUP_EXISTS=$(query_az group exists --name "$1" --output tsv)
    GROUP_LOCATION=""
    GROUP_TAGGED=false
    if [ "$GROUP_EXISTS" = true ]; then
        details=$(query_az group show --name "$1" --query "[location, tags.\"created-by\"]" --output tsv)
        GROUP_LOCATION=$(line 1 "$details")
        if [ "$(line 2 "$details")" = "${WORKSHOP_TAG#*=}" ]; then
            GROUP_TAGGED=true
        fi
    fi
}

ensure_group() {
    local group=$1 location=$2
    if [ "$GROUP_EXISTS" = true ]; then
        info "Using resource group $group in $GROUP_LOCATION."
        return 0
    fi
    doing "Creating" "create" "resource group $group in $location"
    run_az group create --name "$group" --location "$location" --tags "$WORKSHOP_TAG"
    GROUP_CREATED=true
    if [ "$DRY_RUN" != true ]; then
        GROUP_EXISTS=true
    fi
}

# Prints the plan's pricing tier, or nothing when the plan doesn't exist.
plan_sku() {
    if [ "$GROUP_EXISTS" = true ]; then
        query_az appservice plan list --resource-group "$1" --query "[?name=='$2'] | [0].sku.name" --output tsv
    fi
}

ensure_plan() {
    local group=$1 plan=$2 location=$3 sku=$4 sku_given=$5 existing
    existing=$(plan_sku "$group" "$plan")
    if [ -n "$existing" ]; then
        info "Using App Service plan $plan ($existing)."
        if [ "$sku_given" = true ] && [ "$existing" != "$sku" ]; then
            info "--sku only applies to new plans. To move $plan to $sku, run: az appservice plan update --name $plan --resource-group $group --sku $sku"
        fi
        return 0
    fi
    doing "Creating" "create" "App Service plan $plan ($sku, Linux) in $location"
    run_az appservice plan create --name "$plan" --resource-group "$group" --location "$location" --is-linux --sku "$sku" --tags "$WORKSHOP_TAG"
}

inspect_webapp() {
    local group=$1 app=$2 details=""
    WEBAPP_EXISTS=false
    WEBAPP_ID="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$group/providers/Microsoft.Web/sites/$app"
    WEBAPP_HOST="$app.azurewebsites.net"
    if [ "$GROUP_EXISTS" = true ]; then
        details=$(query_az webapp list --resource-group "$group" --query "[?name=='$app'] | [0].[id, defaultHostName]" --output tsv)
    fi
    if [ -n "$details" ]; then
        WEBAPP_EXISTS=true
        WEBAPP_ID=$(line 1 "$details")
        WEBAPP_HOST=$(line 2 "$details")
    fi
}

ensure_webapp() {
    local group=$1 app=$2 plan=$3
    if [ "$WEBAPP_EXISTS" = true ]; then
        info "Using web app $app."
        return 0
    fi
    doing "Creating" "create" "web app $app with the $WEBAPP_RUNTIME runtime"
    run_az webapp create --name "$app" --resource-group "$group" --plan "$plan" --runtime "$WEBAPP_RUNTIME" --https-only true --basic-auth Disabled --tags "$WORKSHOP_TAG"
    if [ "$DRY_RUN" != true ]; then
        inspect_webapp "$group" "$app"
    fi
}

inspect_identity() {
    local group=$1 identity=$2 details=""
    IDENTITY_EXISTS=false
    IDENTITY_CLIENT_ID="<client-id-of-$identity>"
    IDENTITY_PRINCIPAL_ID="<principal-id-of-$identity>"
    if [ "$GROUP_EXISTS" = true ]; then
        details=$(query_az identity list --resource-group "$group" --query "[?name=='$identity'] | [0].[clientId, principalId]" --output tsv)
    fi
    if [ -n "$details" ]; then
        IDENTITY_EXISTS=true
        IDENTITY_CLIENT_ID=$(line 1 "$details")
        IDENTITY_PRINCIPAL_ID=$(line 2 "$details")
    fi
}

ensure_identity() {
    local group=$1 identity=$2 location=$3
    if [ "$IDENTITY_EXISTS" = true ]; then
        info "Using managed identity $identity."
        return 0
    fi
    doing "Creating" "create" "user-assigned managed identity $identity"
    run_az identity create --name "$identity" --resource-group "$group" --location "$location" --tags "$WORKSHOP_TAG"
    if [ "$DRY_RUN" != true ]; then
        inspect_identity "$group" "$identity"
        if [ "$IDENTITY_EXISTS" != true ]; then
            die "Azure created $identity, but the script can't find it yet. Wait a minute, then run the script again."
        fi
    fi
}

# Makes the identity trust tokens whose subject is SUBJECT, through a federated credential called NAME.
ensure_credential() {
    local group=$1 identity=$2 name=$3 subject=$4 existing="" row_name row_subject current="" other=""
    if [ "$IDENTITY_EXISTS" = true ]; then
        existing=$(query_az identity federated-credential list --identity-name "$identity" --resource-group "$group" --query "[].[name, subject]" --output tsv)
    fi
    while IFS=$'\t' read -r row_name row_subject; do
        if [ "$row_name" = "$name" ]; then
            current=$row_subject
        elif [ -n "$row_name" ] && [ "$row_subject" = "$subject" ]; then
            other=$row_name
        fi
    done < <(printf '%s\n' "$existing")
    if [ "$current" = "$subject" ]; then
        info "Federated credential $name already trusts $subject."
    elif [ -n "$current" ]; then
        doing "Updating" "update" "federated credential $name to trust $subject instead of $current"
        run_az identity federated-credential update --identity-name "$identity" --resource-group "$group" --name "$name" --issuer "$OIDC_ISSUER" --subject "$subject" --audiences "$OIDC_AUDIENCE"
    elif [ -n "$other" ]; then
        info "Federated credential $other already trusts $subject."
    else
        doing "Creating" "create" "federated credential $name for $subject"
        run_az identity federated-credential create --identity-name "$identity" --resource-group "$group" --name "$name" --issuer "$OIDC_ISSUER" --subject "$subject" --audiences "$OIDC_AUDIENCE"
    fi
}

ensure_role() {
    local identity=$1 app=$2 count=0
    if [ "$IDENTITY_EXISTS" = true ] && [ "$WEBAPP_EXISTS" = true ]; then
        count=$(query_az role assignment list --assignee-object-id "$IDENTITY_PRINCIPAL_ID" --role "$DEPLOY_ROLE" --scope "$WEBAPP_ID" --fill-principal-name false --query "length(@)" --output tsv)
    fi
    if [ "${count:-0}" -gt 0 ]; then
        info "$identity already has the $DEPLOY_ROLE role on $app."
        return 0
    fi
    doing "Giving" "give" "$identity the $DEPLOY_ROLE role on web app $app, and nothing else"
    print_cmd role assignment create --assignee-object-id "$IDENTITY_PRINCIPAL_ID" --assignee-principal-type ServicePrincipal --role "$DEPLOY_ROLE" --scope "$WEBAPP_ID"
    if [ "$DRY_RUN" = true ]; then
        return 0
    fi
    if ! az role assignment create --assignee-object-id "$IDENTITY_PRINCIPAL_ID" --assignee-principal-type ServicePrincipal --role "$DEPLOY_ROLE" --scope "$WEBAPP_ID" >/dev/null; then
        die "Azure didn't let you assign the role. That needs Owner, User Access Administrator, or Role Based Access Control Administrator on the subscription, the resource group, or the web app. Ask someone with one of those roles to run the command above, or to give you one of them, then run this script again. It reuses what it already created."
    fi
}

# Summary

print_gh_commands() {
    local repo=$1 client_id=$2 app=$3
    printf '  gh variable set %s --repo %s --body "%s"\n' \
        AZURE_CLIENT_ID "$repo" "$client_id" \
        AZURE_TENANT_ID "$repo" "$TENANT_ID" \
        AZURE_SUBSCRIPTION_ID "$repo" "$SUBSCRIPTION_ID" \
        AZURE_WEBAPP_NAME "$repo" "$app"
}

print_browser_steps() {
    section "To add them in the browser instead:"
    info "  1. In the repository on GitHub, select Settings."
    info "  2. In the sidebar, select Secrets and variables, then Actions."
    info "  3. On the Variables tab, select New repository variable."
    info "  4. Enter the name and the value, then select Add variable. Repeat for each variable."
}

# Prints the four repository variables, the site, and how to set the variables.
print_variables() {
    local repo=$1 client_id=$2 app=$3 site=$4
    section "Add these repository variables to $repo:"
    printf '  %-22s %s\n' \
        AZURE_CLIENT_ID "$client_id" \
        AZURE_TENANT_ID "$TENANT_ID" \
        AZURE_SUBSCRIPTION_ID "$SUBSCRIPTION_ID" \
        AZURE_WEBAPP_NAME "$app"
    info "Site: $site"
    section "To add them with the GitHub CLI:"
    print_gh_commands "$repo" "$client_id" "$app"
    print_browser_steps
}

# Prints how to delete what the script created. A resource group the scripts didn't create may hold other
# resources, so in that case it lists the commands that delete only this app's resources.
print_cleanup() {
    local group=$1 app=$2 identity=$3
    section "To delete everything this script created:"
    if [ "$GROUP_TAGGED" = true ] || [ "$GROUP_CREATED" = true ]; then
        print_cmd group delete --name "$group"
    else
        print_cmd role assignment delete --assignee-object-id "$IDENTITY_PRINCIPAL_ID" --role "$DEPLOY_ROLE" --scope "$WEBAPP_ID"
        print_cmd identity delete --name "$identity" --resource-group "$group"
        print_cmd webapp delete --name "$app" --resource-group "$group"
    fi
}

finish() {
    if [ "$DRY_RUN" = true ]; then
        section "Dry run finished. Nothing was created or changed."
    fi
}

# The steps learner-setup.sh runs. It sets these variables first:
# APP_NAME, REPO, RESOURCE_GROUP, LOCATION, SKU, SKU_GIVEN, CRED_NAMES, and CRED_SUBJECTS.
run_app_setup() {
    local identity="id-$APP_NAME" plan="plan-$APP_NAME" i
    inspect_group "$RESOURCE_GROUP"
    if [ -z "$LOCATION" ]; then
        LOCATION=${GROUP_LOCATION:-$DEFAULT_LOCATION}
    fi
    inspect_webapp "$RESOURCE_GROUP" "$APP_NAME"
    inspect_identity "$RESOURCE_GROUP" "$identity"
    require_usable_app_name
    ensure_providers Microsoft.Web Microsoft.ManagedIdentity

    section "Resources:"
    ensure_group "$RESOURCE_GROUP" "$LOCATION"
    if [ "$WEBAPP_EXISTS" != true ]; then
        ensure_plan "$RESOURCE_GROUP" "$plan" "$LOCATION" "$SKU" "$SKU_GIVEN"
    fi
    ensure_webapp "$RESOURCE_GROUP" "$APP_NAME" "$plan"
    ensure_identity "$RESOURCE_GROUP" "$identity" "$LOCATION"
    for i in "${!CRED_NAMES[@]}"; do
        ensure_credential "$RESOURCE_GROUP" "$identity" "${CRED_NAMES[$i]}" "${CRED_SUBJECTS[$i]}"
    done
    ensure_role "$identity" "$APP_NAME"

    print_variables "$REPO" "$IDENTITY_CLIENT_ID" "$APP_NAME" "https://$WEBAPP_HOST"
    section "$identity trusts tokens with these subjects:"
    for i in "${!CRED_NAMES[@]}"; do
        printf '  %-24s %s\n' "${CRED_NAMES[$i]}" "${CRED_SUBJECTS[$i]}"
    done
    print_cleanup "$RESOURCE_GROUP" "$APP_NAME" "$identity"
    finish
}
