# Design System Strategy: The Cinematic Lens

## 1. Overview & Creative North Star
**Creative North Star: "The Noir Concierge"**
This design system moves away from the "utility software" aesthetic and toward a premium, cinematic experience. It treats cinema management not as a series of database entries, but as a high-end production. The interface utilizes a "deep-stage" approach: high-contrast typography mimics film credits, while deep charcoal and navy surfaces create a low-light environment reminiscent of a darkened theater. 

To break the "template" look, we utilize **Intentional Asymmetry**. Large-scale data (like daily revenue) is presented with aggressive, editorial typography (`display-lg`), while secondary navigation is tucked into ultra-minimalist, low-contrast containers. We favor **Tonal Depth** over lines, creating a UI that feels carved out of light and shadow rather than built with blocks.

---

## 2. Colors: Depth & Emphasis
The palette is built on a foundation of "Midnight" neutrals to ensure the vibrant "Cinema Red" and "Golden Age" accents feel intentional and high-stakes.

*   **Primary Foundation:** Use `surface` (#060e20) for the overall "dark room" environment.
*   **The "No-Line" Rule:** Standard 1px borders are strictly prohibited for sectioning. To separate a sidebar from a main content area, transition from `surface-container-low` (#091328) to `surface` (#060e20). Let the change in value define the boundary.
*   **Surface Hierarchy & Nesting:** 
    *   **Base Layer:** `surface` (#060e20)
    *   **Sectioning:** `surface-container-low` (#091328) for large layout blocks.
    *   **Interactive Cards:** `surface-container-high` (#141f38) to "lift" the content toward the user.
*   **The Glass & Gradient Rule:** For floating modals or "Now Playing" overlays, use `surface-bright` (#1f2b49) at 60% opacity with a `20px` backdrop-blur. Apply a subtle linear gradient from `primary` (#ff8c94) to `primary-container` (#ff7481) on high-level CTAs to simulate the glow of a neon marquee.

---

## 3. Typography: The Editorial Edge
We use a dual-sans-serif pairing to balance high-fashion "Display" with "Technical" utility.

*   **Headlines (Manrope):** The "Leading Lady." Use `display-lg` for hero metrics (e.g., "94% Occupancy"). The wide apertures of Manrope feel modern and expansive.
*   **Body & UI (Inter):** The "Supporting Cast." Inter provides maximum legibility for data-heavy tables. Use `body-md` for standard data and `label-sm` in `on-surface-variant` (#a3aac4) for metadata.
*   **Hierarchy Note:** Always pair a `headline-sm` in `on-surface` with a `label-md` in `secondary` (#f8a010) for categorized headers. This "Gold on Charcoal" look mimics premium theater branding.

---

## 4. Elevation & Depth: Tonal Layering
In this system, light is the architect. We do not use traditional shadows to "drop" elements; we use them to "glow."

*   **The Layering Principle:** To create a data table, place the table header on `surface-container-highest` (#192540) and the rows on `surface-container-high` (#141f38). The shift in tone creates natural separation.
*   **Ambient Shadows:** For floating elements like seat-selection popovers, use a wide-spread shadow: `offset-y: 16px, blur: 40px, color: rgba(0, 0, 0, 0.4)`. 
*   **The "Ghost Border" Fallback:** If a container requires a border (e.g., an input field), use `outline-variant` (#40485d) at **20% opacity**. It should be felt, not seen.
*   **Glassmorphism:** Use for "Quick View" panels. A `surface-variant` background with a blur allows the vibrant secondary/tertiary colors of movie posters to bleed through the UI, making the dashboard feel alive.

---

## 5. Components: Practical Sophistication

### Buttons
*   **Primary (Action):** `primary` (#ff8c94) background with `on-primary` (#640018) text. Use `rounded-md` (0.375rem). Use a subtle inner-glow gradient for a "pressed" state.
*   **Secondary (Navigation):** `surface-container-highest` (#192540) with a `ghost-border` of `primary` at 30%.

### Data Tables & Lists
*   **Forbid Dividers:** Do not use lines between rows. Use `spacing-2` (0.4rem) of vertical padding and alternating row colors (using `surface-container-low` vs `surface-container-high`).
*   **Status Indicators:** Use `tertiary` (#47c4ff) for "Scheduled" and `secondary` (#f8a010) for "Selling Out."

### Input Fields
*   **Text Inputs:** Use `surface-container-lowest` (#000000) for the field background to create a "recessed" look. The label should sit in `label-md` directly above the field in `on-surface-variant`.

### Cinema-Specific Components
*   **The Marquee Card:** A card for movie titles using `surface-container-highest`. Use an image mask that allows the movie poster to fade into the `surface` color using a CSS mask-image gradient.
*   **Occupancy Gauge:** Use a thick `secondary` (#f8a010) stroke on a `surface-variant` track to represent theater filling levels.

---

## 6. Do’s and Don’ts

### Do:
*   **Do** use `spacing-16` and `spacing-20` for generous margins between major dashboard widgets to prevent "data fatigue."
*   **Do** use `secondary_fixed` (#ffc885) for "VIP" or "Gold Class" indicators to provide a distinct premium feel.
*   **Do** rely on the `on-surface-variant` (#a3aac4) for all non-essential text to keep the visual noise low in data-heavy screens.

### Don’t:
*   **Don’t** use pure white (#FFFFFF) for text. Always use `on-surface` (#dee5ff) to reduce eye strain in dark environments.
*   **Don’t** use standard 1px borders to separate table columns. Use horizontal white space (`spacing-8`).
*   **Don’t** use bright, saturated red for errors. Use `error_dim` (#d7383b) to ensure the UI remains sophisticated and doesn't feel "alarming" unless necessary.