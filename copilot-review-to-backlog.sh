#!/bin/bash

# Script to extract GitHub Copilot PR review suggestions and append to BACKLOG.md
# Usage: ./copilot-review-to-backlog.sh <PR_NUMBER>

set -e
set -o pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
pushd "$SCRIPT_DIR" > /dev/null

PR_NUMBER=$1
# Use absolute path to BACKLOG.md
BACKLOG_FILE="$HOME/Documents/Orion/Projects/Nine/Roadmap/BACKLOG.md"

if [ -z "$PR_NUMBER" ]; then
    echo "Usage: $0 <PR_NUMBER>"
    echo "Example: $0 123"
    exit 1
fi

# Check if gh CLI is installed
if ! command -v gh &> /dev/null; then
    echo "Error: GitHub CLI (gh) is not installed."
    echo "Install with: sudo dnf install gh"
    exit 1
fi

# Check if jq is installed (used for parsing JSON from gh api)
if ! command -v jq &> /dev/null; then
    echo "Error: jq is not installed."
    echo "Install with: sudo dnf install jq"
    exit 1
fi

# Check if user is authenticated
if ! gh auth status &> /dev/null; then
    echo "Error: Not authenticated with GitHub CLI"
    echo "Run: gh auth login"
    exit 1
fi

# Get repository info
REPO=$(gh repo view --json nameWithOwner -q .nameWithOwner)
PR_TITLE=$(gh pr view $PR_NUMBER --json title -q .title)
PR_URL=$(gh pr view $PR_NUMBER --json url -q .url)

# GitHub uses different usernames for Copilot depending on endpoint/context.
# Examples seen in the wild: "Copilot", "copilot-pull-request-reviewer", "github-copilot[bot]".
COPILOT_LOGIN_REGEX='^(Copilot|copilot-pull-request-reviewer|github-copilot\[bot\])$'

echo "Fetching Copilot review suggestions from PR #$PR_NUMBER..."

# Debug: Check what review authors exist
echo "Debug: Checking review authors..."
gh pr view $PR_NUMBER --json reviews --jq '.reviews[].author.login' | sort -u

# Fetch Copilot review comments (copilot-pull-request-reviewer is the actual bot name)
# Format: File, Line, Body, and code suggestion (if present)
COMMENTS=$(gh api repos/$REPO/pulls/$PR_NUMBER/comments | jq -r --arg re "$COPILOT_LOGIN_REGEX" '
    .[]
    | select(.user.login | test($re; "i"))
    | "**File:** \(.path) (Line \((.line // .original_line // .position // "?")|tostring))\n"
        + (if .html_url then "**Link:** \(.html_url)\n" else "" end)
        + (if .body then "**Comment:**\n" + .body + "\n" else "" end)
        + "---\n"
')

# Fetch Copilot PR conversation comments (e.g., PR overview comment)
PR_COMMENTS=$(gh api repos/$REPO/issues/$PR_NUMBER/comments | jq -r --arg re "$COPILOT_LOGIN_REGEX" '
    .[]
    | select(.user.login | test($re; "i"))
    | "**Link:** \(.html_url)\n"
        + "**Comment:**\n" + (.body // "") + "\n"
        + "---\n"
')

# Fetch Copilot review body (overall review)
REVIEW=$(gh pr view $PR_NUMBER --json reviews | jq -r --arg re "$COPILOT_LOGIN_REGEX" '
    (
        .reviews
        | map(select((.author.login // .user.login // "") | test($re; "i")))
        | first
        | .body
    ) // ""
')

# If still no results, try fetching ALL review comments to see structure
if [ -z "$COMMENTS" ] && [ -z "$PR_COMMENTS" ] && [ -z "$REVIEW" ]; then
    echo "Debug: No Copilot reviews found. Checking all reviews..."
    gh api repos/$REPO/pulls/$PR_NUMBER/reviews --jq '.[] | {author: .user.login, body: .body}' | head -20
fi

if [ -z "$COMMENTS" ] && [ -z "$PR_COMMENTS" ] && [ -z "$REVIEW" ]; then
    echo "No Copilot review suggestions found on PR #$PR_NUMBER"
    exit 0
fi

# Prepare backlog entry
TIMESTAMP=$(date +"%Y-%m-%d %H:%M:%S")
ENTRY="
## GitHub Copilot Review - PR #$PR_NUMBER
**Date:** $TIMESTAMP  
**PR:** [$PR_TITLE]($PR_URL)

"

if [ -n "$REVIEW" ]; then
    ENTRY+="### Overall Review

$REVIEW

"
fi

if [ -n "$PR_COMMENTS" ]; then
    ENTRY+="### PR Conversation Comments

$PR_COMMENTS
"
fi

if [ -n "$COMMENTS" ]; then
    ENTRY+="### Inline Suggestions

$COMMENTS
"
fi

ENTRY+="---

"

# Append to BACKLOG.md
if [ ! -f "$BACKLOG_FILE" ]; then
    echo "Error: BACKLOG.md not found at $BACKLOG_FILE"
    exit 1
fi

echo "$ENTRY" >> "$BACKLOG_FILE"

echo "✅ Copilot review suggestions appended to BACKLOG.md"
echo "📄 Review PR #$PR_NUMBER at: $PR_URL"
echo "📝 Backlog updated: $BACKLOG_FILE"

popd > /dev/null
