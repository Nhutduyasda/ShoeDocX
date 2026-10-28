"""Reuse the actual SQL/Identity Office journey, independent of historical gate verdicts."""
from pathlib import Path
from types import SimpleNamespace
import gate5d_browser
root=Path(__file__).resolve().parents[2]
evidence=root/'.audit-run/ui-v2/office'
evidence.mkdir(parents=True,exist_ok=True)
gate5d_browser.browser(SimpleNamespace(ROOT=root.parent,FEATURE=root,EVIDENCE=evidence))
