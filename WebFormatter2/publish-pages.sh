#!/usr/bin/env sh
set -e

# Publish the WebFormatter to the GitHub Pages docs/ folder, including a
# reserved /preview/ slot that never overwrites the official site root.
#
# The github-pages branch contains only docs/, so this script disappears from
# the working tree after you check that branch out. `build` therefore writes a
# copy of itself (and the no-preview stub) into the staging directory, which
# lives outside the repo and survives the checkout.

usage() {
    cat <<'EOF'
Usage (from the repo root or WebFormatter2/):
  ./WebFormatter2/publish-pages.sh build
  ./WebFormatter2/publish-pages.sh prod
  ./WebFormatter2/publish-pages.sh preview
  ./WebFormatter2/publish-pages.sh stub

Commands:
  build     dotnet publish into the staging dir (run on main / a feature branch)
  prod      copy staging wwwroot -> docs/, preserving docs/preview/
  preview   copy staging wwwroot -> docs/preview/
  stub      replace docs/preview/ with the "no preview right now" page

Options:
  -o, --out DIR   staging dir (default: ~/code/build/fjweb)
  --docs DIR      GitHub Pages docs folder (default: ./docs if that exists)

Typical production update:
  1. On main:    ./WebFormatter2/publish-pages.sh build
  2.             git checkout github-pages && git checkout -b github-pages-new
  3.             ~/code/build/fjweb/publish-pages.sh prod
  4. Commit, push, switch Pages to the new branch, test, merge back.

Typical preview update (feature branch already checked out):
  1. ./WebFormatter2/publish-pages.sh build
  2. git checkout github-pages
  3. ~/code/build/fjweb/publish-pages.sh preview
  4. Commit and push. This does not touch the official site root.

When the preview is done, on github-pages:
  ~/code/build/fjweb/publish-pages.sh stub

`prod` never deletes docs/preview/. If preview/ is missing, the no-preview
stub is installed so old /preview links stay friendly.

Do not run prod/preview/stub from main unless you pass --docs: that would
create a docs/ folder on the source branch. After `build`, checkout
github-pages and run the copy of this script that `build` left in staging.

Optional alternative: keep main checked out and add a worktree
(`git worktree add ~/code/fj-pages github-pages`), then
`./WebFormatter2/publish-pages.sh prod --docs ~/code/fj-pages/docs`.
EOF
    exit "${1:-0}"
}

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
if [ -f "$SCRIPT_DIR/WebFormatter2.csproj" ]; then
    IN_SOURCE=1
    REPO_ROOT=$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd)
    DEFAULT_STAGING="$HOME/code/build/fjweb"
else
    # Copy of this script that `build` dropped into the staging directory.
    IN_SOURCE=0
    REPO_ROOT=""
    DEFAULT_STAGING="$SCRIPT_DIR"
fi

CMD=""
STAGING=""
STAGING_EXPLICIT=0
DOCS=""
DOCS_EXPLICIT=0

while [ "$#" -gt 0 ]; do
    case "$1" in
        -h|--help)
            usage 0
            ;;
        -o|--out)
            [ "$#" -ge 2 ] || { echo "error: $1 requires a directory" >&2; exit 2; }
            STAGING=$2
            STAGING_EXPLICIT=1
            shift 2
            ;;
        --docs)
            [ "$#" -ge 2 ] || { echo "error: $1 requires a directory" >&2; exit 2; }
            DOCS=$2
            DOCS_EXPLICIT=1
            shift 2
            ;;
        -*)
            echo "error: unknown option: $1" >&2
            usage 2
            ;;
        *)
            if [ -n "$CMD" ]; then
                echo "error: unexpected argument: $1" >&2
                usage 2
            fi
            CMD=$1
            shift
            ;;
    esac
done

[ -n "$CMD" ] || { echo "error: missing command (build|prod|preview|stub)" >&2; usage 2; }

if [ "$STAGING_EXPLICIT" -eq 0 ]; then
    STAGING=$DEFAULT_STAGING
fi
WWWROOT="$STAGING/wwwroot"

if [ -d "$SCRIPT_DIR/preview-stub" ]; then
    STUB="$SCRIPT_DIR/preview-stub"
else
    STUB="$STAGING/preview-stub"
fi

if [ "$DOCS_EXPLICIT" -eq 0 ]; then
    if [ -d "$PWD/docs" ]; then
        DOCS="$PWD/docs"
    elif [ "$IN_SOURCE" -eq 1 ] && [ -d "$REPO_ROOT/docs" ]; then
        DOCS="$REPO_ROOT/docs"
    else
        DOCS=""
    fi
fi

ensure_docs() {
    if [ -z "$DOCS" ]; then
        echo "error: no docs/ folder in the current directory." >&2
        echo "The github-pages branch is the one with docs/. After building, run:" >&2
        echo "  git checkout github-pages" >&2
        echo "  $STAGING/publish-pages.sh $CMD" >&2
        echo "Or pass --docs /path/to/docs (for example a git worktree)." >&2
        exit 1
    fi
    mkdir -p "$DOCS"
}

ensure_wwwroot() {
    if [ ! -d "$WWWROOT" ]; then
        echo "error: published wwwroot not found: $WWWROOT" >&2
        echo "Run './WebFormatter2/publish-pages.sh build' on a source branch first." >&2
        exit 1
    fi
}

ensure_stub() {
    if [ ! -f "$STUB/index.html" ]; then
        echo "error: preview stub not found: $STUB/index.html" >&2
        exit 1
    fi
}

install_stub() {
    ensure_docs
    ensure_stub
    rm -rf "$DOCS/preview"
    mkdir -p "$DOCS/preview"
    cp "$STUB/index.html" "$DOCS/preview/index.html"
    echo "Installed no-preview stub at $DOCS/preview/"
}

cmd_build() {
    if [ "$IN_SOURCE" -eq 0 ]; then
        echo "error: build must be run from WebFormatter2 on a source branch, not from staging." >&2
        exit 1
    fi
    echo "Publishing WebFormatter2 to $STAGING"
    rm -rf "$STAGING"
    dotnet publish "$SCRIPT_DIR/WebFormatter2.csproj" -c Release -o "$STAGING"
    mkdir -p "$STAGING/preview-stub"
    cp "$0" "$STAGING/publish-pages.sh"
    chmod +x "$STAGING/publish-pages.sh"
    cp "$SCRIPT_DIR/preview-stub/index.html" "$STAGING/preview-stub/index.html"
    echo "Build staged at $STAGING"
    echo "The github-pages branch does not contain this script. After you check it out, run:"
    echo "  $STAGING/publish-pages.sh prod      # official site, keeps preview/"
    echo "  $STAGING/publish-pages.sh preview   # live preview at /preview/"
    echo "  $STAGING/publish-pages.sh stub      # no-preview placeholder"
}

cmd_prod() {
    ensure_docs
    ensure_wwwroot
    if ! command -v rsync >/dev/null 2>&1; then
        echo "error: rsync is required so prod can update docs/ without deleting preview/" >&2
        exit 1
    fi
    # Trailing slashes: copy contents of wwwroot into docs, delete stale files,
    # but never touch the reserved preview slot.
    rsync -a --delete --exclude '/preview/' --exclude '/preview' "$WWWROOT/" "$DOCS/"
    # GitHub Pages treats _framework as a Jekyll draft unless this file exists.
    if [ ! -e "$DOCS/.nojekyll" ]; then
        if [ -f "$WWWROOT/.nojekyll" ]; then
            cp "$WWWROOT/.nojekyll" "$DOCS/.nojekyll"
        else
            printf '%s\n' "This exists to keep GitHub from getting confused by directory names beginning with underscores." > "$DOCS/.nojekyll"
        fi
    fi
    if [ ! -e "$DOCS/index.html" ]; then
        echo "warning: $DOCS/index.html is missing after copy." >&2
    fi
    if [ ! -e "$DOCS/preview/index.html" ]; then
        echo "No preview slot found; installing the no-preview stub."
        install_stub
    fi
    echo "Official site copied to $DOCS/ (preview/ preserved)"
}

cmd_preview() {
    ensure_docs
    ensure_wwwroot
    if ! command -v rsync >/dev/null 2>&1; then
        echo "error: rsync is required" >&2
        exit 1
    fi
    if [ ! -e "$DOCS/index.html" ]; then
        echo "warning: $DOCS/index.html is missing; preview will exist without an official site root." >&2
    fi
    mkdir -p "$DOCS/preview"
    rsync -a --delete "$WWWROOT/" "$DOCS/preview/"
    echo "Preview app copied to $DOCS/preview/"
}

case "$CMD" in
    build)   cmd_build ;;
    prod)    cmd_prod ;;
    preview) cmd_preview ;;
    stub)    install_stub ;;
    *)
        echo "error: unknown command: $CMD" >&2
        usage 2
        ;;
esac
