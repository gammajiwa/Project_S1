#!/bin/bash
# Claude Code Stop hook: Log session summary when Claude finishes
# Records what was worked on for audit trail and sprint tracking

TIMESTAMP=$(date +%Y%m%d_%H%M%S)
SESSION_LOG_DIR="production/session-logs"

mkdir -p "$SESSION_LOG_DIR" 2>/dev/null

# --- ROTASI: log dipangkas berdasarkan UKURAN, bukan jam ---
# Stop hook menyala TIAP giliran jawab, bukan cuma saat sesi tutup — log yang
# tumbuh per giliran harus dipagari di sini juga, di penulisnya. Melewati 5 MB,
# isi lama digeser ke session-log.old.md (satu generasi disimpan buat forensik);
# total disk terkunci ~10 MB, tidak peduli sepanjang apa harinya.
LOG_FILE="$SESSION_LOG_DIR/session-log.md"
MAX_BYTES=$((5 * 1024 * 1024))
if [ -f "$LOG_FILE" ]; then
    SIZE=$(wc -c < "$LOG_FILE" 2>/dev/null || echo 0)
    if [ "$SIZE" -gt "$MAX_BYTES" ]; then
        mv -f "$LOG_FILE" "$SESSION_LOG_DIR/session-log.old.md" 2>/dev/null
        echo "# (dirotasi $TIMESTAMP — isi sebelumnya di session-log.old.md)" > "$LOG_FILE"
    fi
fi

# Log recent git activity from this session (check up to 8 hours for long sessions)
RECENT_COMMITS=$(git log --oneline --since="8 hours ago" 2>/dev/null)
MODIFIED_FILES=$(git diff --name-only 2>/dev/null)

# --- Jejak state sesi: POINTER, bukan salinan ---
# Dulu blok ini meng-cat SELURUH active.md (ratusan KB) ke log ini TIAP giliran —
# itulah yang membuat log 108 MB dalam sehari. active.md sendiri adalah arsip yang
# persisten dan tidak pernah dihapus; menyalinnya berulang tidak mengarsipkan
# apa-apa, cuma menggandakan. Yang berguna bagi audit cukup: kapan, seberapa
# besar, dan kapan terakhir berubah.
STATE_FILE="production/session-state/active.md"
if [ -f "$STATE_FILE" ]; then
    STATE_LINES=$(wc -l < "$STATE_FILE" 2>/dev/null || echo "?")
    STATE_MTIME=$(date -r "$STATE_FILE" +%Y%m%d_%H%M%S 2>/dev/null || echo "?")
    echo "## State: $TIMESTAMP | active.md $STATE_LINES baris, terakhir berubah $STATE_MTIME" \
        >> "$LOG_FILE" 2>/dev/null
fi

if [ -n "$RECENT_COMMITS" ] || [ -n "$MODIFIED_FILES" ]; then
    {
        echo "## Session End: $TIMESTAMP"
        if [ -n "$RECENT_COMMITS" ]; then
            echo "### Commits"
            echo "$RECENT_COMMITS"
        fi
        if [ -n "$MODIFIED_FILES" ]; then
            echo "### Uncommitted Changes"
            echo "$MODIFIED_FILES"
        fi
        echo "---"
        echo ""
    } >> "$SESSION_LOG_DIR/session-log.md" 2>/dev/null
fi

exit 0
