"""Print bounded summaries of saved synthetic browser failures and trace calls.

Read-only offline inspection: no navigation, model mutation, or dependency install.
Editor text is limited to a short prefix to identify the synthetic test phase.
"""
import json
import sys
import zipfile
from pathlib import Path

root = Path(sys.argv[1] if len(sys.argv) > 1 else "artifacts/evidence")
for path in sorted(root.rglob("failure-*.json")):
    if path.stat().st_size > 20_000_000:
        print("SKIP oversized evidence", path)
        continue
    data = json.loads(path.read_text(encoding="utf-8-sig"))
    state = data.get("state") or {}
    editors = {}
    for key, editor in (state.get("editors") or {}).items():
        if key not in ("main", "code", "preview"):
            continue
        editors[key] = {k: v for k, v in editor.items() if k != "text"}
        text = editor.get("text") or ""
        editors[key].update(textLength=len(text), textPrefix=text[:100])
    bounds = {k: v for k, v in (state.get("bounds") or {}).items()
              if k in ("editor:code", "tab:code", "window-title:code", "window:code:minimize", "window:code:restore", "window:code:maximize")}
    summary = {"file": str(path), "stateKeys": list(state), "browserFocus": data.get("browserFocus"),
               "active": state.get("activeWindowId"), "focusedId": state.get("focusedId"),
               "generation": state.get("workspaceGeneration"), "sequence": state.get("sequence"),
               "switcher": state.get("switcherInputScoped"), "editors": editors, "bounds": bounds,
               "windows": [w for w in state.get("windows", []) if w.get("id") in ("code", "main", "explorer")],
               "mdiFrames": state.get("mdiFrames"), "inputTrace": state.get("inputTrace")}
    print("FAILURE_STATE " + json.dumps(summary, ensure_ascii=True)[:24000])

for path in sorted(root.rglob("trace.zip")):
    with zipfile.ZipFile(path) as archive:
        for name in archive.namelist():
            if not name.endswith(".trace") or archive.getinfo(name).file_size > 60_000_000:
                continue
            pending = {}
            recent = []
            for line in archive.read(name).splitlines():
                event = json.loads(line)
                if event.get("type") == "before":
                    call = {k: event.get(k) for k in ("callId", "apiName", "class", "method", "params", "stack")}
                    pending[event.get("callId")] = call
                    recent.append(call)
                    recent = recent[-8:]
                elif event.get("type") == "after":
                    before = pending.pop(event.get("callId"), None)
                    if event.get("error"):
                        print("FAILED_TRACE_CALL " + json.dumps({"file": str(path), "trace": name,
                            "before": before, "error": event["error"], "recent": recent}, ensure_ascii=True)[:22000])
