#!/usr/bin/env bash
# Sets up Azure for Labs 7 and 8 for a whole class. Run it with --help to see the options.
set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
# shellcheck source-path=SCRIPTDIR source=common.sh
. "$SCRIPT_DIR/common.sh"

usage() {
    cat <<EOF
Usage: scripts/azure/instructor-setup.sh --learners FILE --prefix PREFIX [options]

Sets up Azure for Labs 7 and 8 for a class, so learners don't need Azure access of their own. Each
learner gets a web app and a user-assigned managed identity. The identity trusts GitHub Actions
jobs from the main branch and the staging environment of that learner's repository, and has the
Website Contributor role on that learner's web app, and nothing else. The web apps share Linux
App Service plans in one resource group. At the end, the script prints each learner's four
repository variables and writes them to a CSV file.

Required:
  --learners FILE          Text file with one learner repository, OWNER/REPO, per line. Blank
                           lines and anything after # are ignored. For a private repository you
                           can't read, add the learner's OIDC subject prefix after the name.
  --prefix PREFIX          Start of every resource name, such as gha-oct26: 2 to 40 lowercase
                           letters, digits, and hyphens, starting with a letter. Web apps are
                           named PREFIX-XXXXXXXX, from a hash of the repository name.

Options:
  --resource-group NAME    Resource group to use or create. Default: rg-PREFIX
  --location REGION        Region for new resources. Default: the resource group's region if
                           the group exists, otherwise $DEFAULT_LOCATION
  --sku F1|B1              Pricing tier for new App Service plans. Default: B1
  --apps-per-plan N        Web apps per App Service plan, 1 to 10. Default: 8
  --output FILE            CSV file for the values. Default: PREFIX-azure-values.csv
  --dry-run                Print the commands that would change something instead of running
                           them. Read-only checks still run, and no CSV file is written.
  -h, --help               Show this help.

Other names: App Service plans plan-PREFIX-1, plan-PREFIX-2, and so on, identities id-APP, and
federated credentials github-branch-main and github-env-staging.

You need Owner, or Contributor plus User Access Administrator or Role Based Access Control
Administrator, on the subscription or on the resource group. See scripts/azure/README.md.
EOF
}

LEARNERS_FILE=""
PREFIX=""
RESOURCE_GROUP=""
LOCATION=""
SKU="B1"
SKU_GIVEN=false
APPS_PER_PLAN=8
OUTPUT=""
PREFIX_HINT="add the learner's prefix after the repository name in the learners file (scripts/azure/README.md shows how to find it)"

while [ $# -gt 0 ]; do
    case $1 in
        --learners) require_value "$@"; LEARNERS_FILE=$2; shift 2 ;;
        --prefix) require_value "$@"; PREFIX=$2; shift 2 ;;
        --resource-group) require_value "$@"; RESOURCE_GROUP=$2; shift 2 ;;
        --location) require_value "$@"; LOCATION=$2; shift 2 ;;
        --sku) require_value "$@"; SKU=$2; SKU_GIVEN=true; shift 2 ;;
        --apps-per-plan) require_value "$@"; APPS_PER_PLAN=$2; shift 2 ;;
        --output) require_value "$@"; OUTPUT=$2; shift 2 ;;
        --dry-run) DRY_RUN=true; shift ;;
        -h | --help) usage; exit 0 ;;
        *) unknown_option "$1" ;;
    esac
done

if [ -z "$LEARNERS_FILE" ] && [ -z "$PREFIX" ]; then
    die "--learners and --prefix are required. Run $0 --help to see the options."
elif [ -z "$LEARNERS_FILE" ]; then
    die "--learners is required. Run $0 --help to see the options."
elif [ -z "$PREFIX" ]; then
    die "--prefix is required. Run $0 --help to see the options."
fi
prefix_re='^[a-z][a-z0-9-]*[a-z0-9]$'
if ! [[ $PREFIX =~ $prefix_re ]] || [ ${#PREFIX} -gt 40 ]; then
    die "\"$PREFIX\" can't be a prefix. Use 2 to 40 lowercase letters, digits, and hyphens, starting with a letter and ending with a letter or digit."
fi
case $APPS_PER_PLAN in
    '' | *[!0-9]*) die "--apps-per-plan must be a number from 1 to 10." ;;
esac
if [ "$APPS_PER_PLAN" -lt 1 ] || [ "$APPS_PER_PLAN" -gt 10 ]; then
    die "--apps-per-plan must be a number from 1 to 10."
fi
if [ ! -f "$LEARNERS_FILE" ] || [ ! -r "$LEARNERS_FILE" ]; then
    die "Can't read the learners file $LEARNERS_FILE."
fi
RESOURCE_GROUP=${RESOURCE_GROUP:-rg-$PREFIX}
validate_resource_group "$RESOURCE_GROUP"
SKU=$(normalize_sku "$SKU")
if [ -n "$LOCATION" ]; then
    LOCATION=$(normalize_location "$LOCATION")
fi
OUTPUT=${OUTPUT:-$PREFIX-azure-values.csv}
if [ -e "$OUTPUT" ] && [ ! -w "$OUTPUT" ]; then
    die "Can't write to $OUTPUT. Choose another file with --output."
elif [ ! -e "$OUTPUT" ] && [ ! -w "$(dirname "$OUTPUT")" ]; then
    die "Can't create $OUTPUT. Choose another file with --output."
fi

# Prints the first 64 characters of the SHA-256 hash of TEXT, in hexadecimal.
sha256_hex() {
    if command -v sha256sum >/dev/null 2>&1; then
        printf '%s' "$1" | sha256sum | cut -c1-64
    elif command -v shasum >/dev/null 2>&1; then
        printf '%s' "$1" | shasum -a 256 | cut -c1-64
    else
        die "This script needs sha256sum or shasum to name the web apps."
    fi
}

contains_line() {
    case $'\n'"$1"$'\n' in
        *$'\n'"$2"$'\n'*) return 0 ;;
    esac
    return 1
}

ERRORS=()
REPOS=()
GIVEN_PREFIXES=()
LINE_NUMBERS=()
seen=""
line_number=0
while IFS= read -r raw || [ -n "$raw" ]; do
    line_number=$((line_number + 1))
    text=$(printf '%s' "${raw%%#*}" | tr -d '\r')
    # Split the line into words on purpose. set -f stops a * from matching file names.
    set -f
    # shellcheck disable=SC2206
    words=($text)
    set +f
    if [ ${#words[@]} -eq 0 ]; then
        continue
    fi
    repo=${words[0]}
    repo_re='^[A-Za-z0-9-]+/[A-Za-z0-9._-]+$'
    if [ ${#words[@]} -gt 2 ]; then
        ERRORS+=("Line $line_number: expected OWNER/REPO, optionally followed by a subject prefix, but found ${#words[@]} words.")
    elif ! [[ $repo =~ $repo_re ]]; then
        ERRORS+=("Line $line_number: \"$repo\" isn't a repository. Use OWNER/REPO.")
    elif contains_line "$seen" "$(to_lower "$repo")"; then
        ERRORS+=("Line $line_number: $repo is already in the file.")
    else
        seen="$seen"$'\n'"$(to_lower "$repo")"
        REPOS+=("$repo")
        GIVEN_PREFIXES+=("${words[1]:-}")
        LINE_NUMBERS+=("$line_number")
    fi
done <"$LEARNERS_FILE"

print_errors_and_stop() {
    local error
    printf 'Error: The script stopped before changing anything:\n' >&2
    for error in "${ERRORS[@]}"; do
        printf '  %s\n' "$error" >&2
    done
    exit 1
}

if [ ${#ERRORS[@]} -gt 0 ]; then
    print_errors_and_stop
fi
if [ ${#REPOS[@]} -eq 0 ]; then
    die "$LEARNERS_FILE doesn't list any learner repositories."
fi

info "Lab 7 and Lab 8 Azure setup for ${#REPOS[@]} learners"
if [ "$DRY_RUN" = true ]; then
    info "Dry run: read-only checks run, and commands that would change something are printed instead."
fi
require_az

section "Checking each learner's repository and web app name:"
APP_NAMES=()
SUBJECT_PREFIXES=()
for i in "${!REPOS[@]}"; do
    repo=${REPOS[$i]}
    APP_NAMES+=("$PREFIX-$(sha256_hex "$(to_lower "$repo")" | cut -c1-8)")
    if [ -n "${GIVEN_PREFIXES[$i]}" ]; then
        PREFIX_ERROR=$(prefix_problem "${GIVEN_PREFIXES[$i]}" "$repo")
        SUBJECT_PREFIX=${GIVEN_PREFIXES[$i]}
    else
        lookup_subject_prefix "$repo"
    fi
    if [ -n "$PREFIX_ERROR" ]; then
        ERRORS+=("Line ${LINE_NUMBERS[$i]}: $PREFIX_ERROR")
        info "  $repo: problem, listed below"
    else
        info "  $repo: web app ${APP_NAMES[$i]}, subject prefix $SUBJECT_PREFIX"
    fi
    SUBJECT_PREFIXES+=("$SUBJECT_PREFIX")
done

inspect_group "$RESOURCE_GROUP"
if [ -z "$LOCATION" ]; then
    LOCATION=${GROUP_LOCATION:-$DEFAULT_LOCATION}
fi
existing_apps=""
if [ "$GROUP_EXISTS" = true ]; then
    existing_apps=$(query_az webapp list --resource-group "$RESOURCE_GROUP" --query "[].name" --output tsv)
fi
unchecked=0
for i in "${!APP_NAMES[@]}"; do
    app=${APP_NAMES[$i]}
    if contains_line "$existing_apps" "$app"; then
        continue
    fi
    check_app_name "$app"
    if [ "$NAME_STATUS" = unknown ]; then
        unchecked=$((unchecked + 1))
    elif [ "$NAME_STATUS" = taken ] && [ -n "$NAME_GROUP" ]; then
        ERRORS+=("Line ${LINE_NUMBERS[$i]}: the web app $app for ${REPOS[$i]} is already in resource group $NAME_GROUP. Use --resource-group $NAME_GROUP.")
    elif [ "$NAME_STATUS" = taken ]; then
        ERRORS+=("Line ${LINE_NUMBERS[$i]}: Azure says the name $app for ${REPOS[$i]} can't be used: $NAME_REASON Choose another --prefix.")
    fi
done
if [ ${#ERRORS[@]} -gt 0 ]; then
    print_errors_and_stop
fi
if [ "$unchecked" -gt 0 ]; then
    warn "Couldn't check whether $unchecked web app names are available, so the script continues. If creating a web app fails because the name is taken, run the script again with another --prefix."
fi
info "All ${#REPOS[@]} repositories and web app names are ready."
ensure_providers Microsoft.Web Microsoft.ManagedIdentity

section "Shared resources:"
ensure_group "$RESOURCE_GROUP" "$LOCATION"

# Existing plans named plan-PREFIX-N, and how many apps each holds.
PLAN_BASE="plan-$PREFIX-"
PLAN_NAMES=()
PLAN_COUNTS=()
LAST_PLAN_NUMBER=0
plan_rows=""
if [ "$GROUP_EXISTS" = true ]; then
    plan_rows=$(query_az appservice plan list --resource-group "$RESOURCE_GROUP" --query "[?starts_with(name, '$PLAN_BASE')].[name, numberOfSites]" --output tsv)
fi
while IFS=$'\t' read -r plan_name plan_count; do
    number=${plan_name#"$PLAN_BASE"}
    case $number in
        '' | *[!0-9]*) continue ;;
    esac
    case $plan_count in
        '' | *[!0-9]*) plan_count=$APPS_PER_PLAN ;;
    esac
    PLAN_NAMES+=("$plan_name")
    PLAN_COUNTS+=("$plan_count")
    info "Found App Service plan $plan_name, which has $plan_count web apps."
    if [ "$number" -gt "$LAST_PLAN_NUMBER" ]; then
        LAST_PLAN_NUMBER=$number
    fi
done < <(printf '%s\n' "$plan_rows")

# Sets PLAN to a plan with room for one more web app, creating a plan when they're all full.
pick_plan() {
    local i
    for i in ${PLAN_NAMES[@]+"${!PLAN_NAMES[@]}"}; do
        if [ "${PLAN_COUNTS[$i]}" -lt "$APPS_PER_PLAN" ]; then
            PLAN=${PLAN_NAMES[$i]}
            PLAN_COUNTS[i]=$((PLAN_COUNTS[i] + 1))
            return 0
        fi
    done
    LAST_PLAN_NUMBER=$((LAST_PLAN_NUMBER + 1))
    PLAN="$PLAN_BASE$LAST_PLAN_NUMBER"
    doing "Creating" "create" "App Service plan $PLAN ($SKU, Linux) in $LOCATION"
    run_az appservice plan create --name "$PLAN" --resource-group "$RESOURCE_GROUP" --location "$LOCATION" --is-linux --sku "$SKU" --tags "$WORKSHOP_TAG"
    PLAN_NAMES+=("$PLAN")
    PLAN_COUNTS+=(1)
}

CLIENT_IDS=()
PRINCIPAL_IDS=()
WEBAPP_IDS=()
SITES=()
for i in "${!REPOS[@]}"; do
    repo=${REPOS[$i]}
    app=${APP_NAMES[$i]}
    identity="id-$app"
    prefix=${SUBJECT_PREFIXES[$i]}
    section "Learner $((i + 1)) of ${#REPOS[@]}: $repo"
    inspect_webapp "$RESOURCE_GROUP" "$app"
    inspect_identity "$RESOURCE_GROUP" "$identity"
    PLAN=""
    if [ "$WEBAPP_EXISTS" != true ]; then
        pick_plan
    fi
    ensure_webapp "$RESOURCE_GROUP" "$app" "$PLAN"
    ensure_identity "$RESOURCE_GROUP" "$identity" "$LOCATION"
    ensure_credential "$RESOURCE_GROUP" "$identity" github-branch-main "$prefix:ref:refs/heads/main"
    ensure_credential "$RESOURCE_GROUP" "$identity" github-env-staging "$prefix:environment:staging"
    ensure_role "$identity" "$app"
    CLIENT_IDS+=("$IDENTITY_CLIENT_ID")
    PRINCIPAL_IDS+=("$IDENTITY_PRINCIPAL_ID")
    WEBAPP_IDS+=("$WEBAPP_ID")
    SITES+=("https://$WEBAPP_HOST")
done

section "Each learner's site and repository variables. Send each learner their block, or their row of the CSV file."
for i in "${!REPOS[@]}"; do
    section "${REPOS[$i]}"
    info "  Site: ${SITES[$i]}"
    print_gh_commands "${REPOS[$i]}" "${CLIENT_IDS[$i]}" "${APP_NAMES[$i]}"
done
print_browser_steps

if [ "$DRY_RUN" = true ]; then
    section "Would write the values for ${#REPOS[@]} learners to $OUTPUT."
else
    {
        printf '%s\n' "repository,AZURE_CLIENT_ID,AZURE_TENANT_ID,AZURE_SUBSCRIPTION_ID,AZURE_WEBAPP_NAME,site_url"
        for i in "${!REPOS[@]}"; do
            printf '%s,%s,%s,%s,%s,%s\n' "${REPOS[$i]}" "${CLIENT_IDS[$i]}" "$TENANT_ID" "$SUBSCRIPTION_ID" "${APP_NAMES[$i]}" "${SITES[$i]}"
        done
    } >"$OUTPUT"
    section "Wrote the values for ${#REPOS[@]} learners to $OUTPUT. Keep it out of the repository, because it maps learners to their web apps."
fi

section "To delete everything this script created, after the workshop:"
if [ "$GROUP_TAGGED" = true ] || [ "$GROUP_CREATED" = true ]; then
    print_cmd group delete --name "$RESOURCE_GROUP"
else
    for i in "${!REPOS[@]}"; do
        print_cmd role assignment delete --assignee-object-id "${PRINCIPAL_IDS[$i]}" --role "$DEPLOY_ROLE" --scope "${WEBAPP_IDS[$i]}"
        print_cmd identity delete --name "id-${APP_NAMES[$i]}" --resource-group "$RESOURCE_GROUP"
        print_cmd webapp delete --name "${APP_NAMES[$i]}" --resource-group "$RESOURCE_GROUP"
    done
fi
finish
