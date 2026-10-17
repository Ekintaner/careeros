# Product presentation system

The existing CareerOS visual language is refined by src/styles/product.css, loaded after the feature styles. It uses shared CSS custom properties rather than a UI package. Neutral green/gray surfaces, readable text, restrained borders and consistent radii apply to existing screens. Desktop pages use a 1200px content limit and 32px padding; mobile uses 16px. The pipeline can use 1600px and scrolls inside its board.

Use semantic headings, actual labels, shared Field/Feedback components, primary/secondary/danger button classes and explicit status text. Loading is role=status; recoverable failures provide actions without replacing entered data. Destructive document confirmation uses ConfirmDialog and native showModal, places focus on Cancel and restores focus when it closes. Reduced motion is supported; state must not depend on animation or color alone.

Controls use a minimum 44px target and visible focus. Compact navigation is native details/summary; destination selection closes it. Below 1100px the pipeline presents one column and a complete stage selector, avoiding hidden-column requests. Desktop shows the board with its own focusable scroll region. Tables may scroll within their container, never widen the whole page.

The Overview uses only the authenticated dashboard snapshot, with useful links, honest empty states and explicit device-timezone dates. Do not add invented activity, decorative percentage changes or nonfunctional shortcuts. See verification-codex013.md for definitions and evidence boundaries.
