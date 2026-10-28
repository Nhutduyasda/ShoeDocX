# UX audit — source baseline

No baseline authenticated screenshots have been captured. This document does not claim visual acceptance.

- Shell duplicates department navigation definitions; XnkManager falls through a presentation branch. Filter all destinations through the existing allowed-tab helper.
- Dashboard quick links can advertise destinations outside the current department; use the same permission source as navigation.
- Dense pages contain many fixed widths, 10–12px labels and inline styles. Preserve grid scrolling and keyboard editing while adding a consistent surrounding hierarchy.
- Login embeds organization copy and lacks an inline failure message. Retain login API and remember-me semantics with labeled native inputs.
- Office Wizard is a long document with six equally weighted horizontal steps. Move progression into a persistent rail and show save/server state nearby; do not infer server approval.
- Wizard upload uses native file controls without drop affordance. Keep native picker and add size/type feedback and replacement/removal.
- Studio tables already expose scroll regions; keep them instead of forcing narrow cells.
- Ant Design widgets remain substantial compatibility areas. A token bridge is necessary; full per-widget migration requires authenticated parity evidence.
- Error, empty and loading states must distinguish unavailable data from a real zero count.
