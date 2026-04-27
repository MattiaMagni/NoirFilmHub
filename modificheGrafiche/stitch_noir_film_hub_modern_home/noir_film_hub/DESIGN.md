---
name: Noir Film Hub
colors:
  surface: '#f8f9fa'
  surface-dim: '#d9dadb'
  surface-bright: '#f8f9fa'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#f3f4f5'
  surface-container: '#edeeef'
  surface-container-high: '#e7e8e9'
  surface-container-highest: '#e1e3e4'
  on-surface: '#191c1d'
  on-surface-variant: '#5e3f3b'
  inverse-surface: '#2e3132'
  inverse-on-surface: '#f0f1f2'
  outline: '#936e69'
  outline-variant: '#e9bcb6'
  surface-tint: '#c0000c'
  primary: '#b8000b'
  on-primary: '#ffffff'
  primary-container: '#e50914'
  on-primary-container: '#fff7f6'
  inverse-primary: '#ffb4aa'
  secondary: '#5f5e5e'
  on-secondary: '#ffffff'
  secondary-container: '#e2dfde'
  on-secondary-container: '#636262'
  tertiary: '#595a5a'
  on-tertiary: '#ffffff'
  tertiary-container: '#727272'
  on-tertiary-container: '#faf8f8'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#ffdad5'
  primary-fixed-dim: '#ffb4aa'
  on-primary-fixed: '#410001'
  on-primary-fixed-variant: '#930007'
  secondary-fixed: '#e5e2e1'
  secondary-fixed-dim: '#c8c6c5'
  on-secondary-fixed: '#1c1b1b'
  on-secondary-fixed-variant: '#474746'
  tertiary-fixed: '#e3e2e2'
  tertiary-fixed-dim: '#c7c6c6'
  on-tertiary-fixed: '#1b1c1c'
  on-tertiary-fixed-variant: '#464747'
  background: '#f8f9fa'
  on-background: '#191c1d'
  surface-variant: '#e1e3e4'
typography:
  display-xl:
    fontFamily: Inter
    fontSize: 72px
    fontWeight: '800'
    lineHeight: '1.1'
    letterSpacing: -0.04em
  headline-lg:
    fontFamily: Inter
    fontSize: 40px
    fontWeight: '700'
    lineHeight: '1.2'
    letterSpacing: -0.02em
  headline-md:
    fontFamily: Inter
    fontSize: 24px
    fontWeight: '700'
    lineHeight: '1.3'
    letterSpacing: -0.01em
  body-lg:
    fontFamily: Inter
    fontSize: 18px
    fontWeight: '400'
    lineHeight: '1.6'
    letterSpacing: '0'
  body-md:
    fontFamily: Inter
    fontSize: 16px
    fontWeight: '400'
    lineHeight: '1.5'
    letterSpacing: '0'
  label-bold:
    fontFamily: Inter
    fontSize: 12px
    fontWeight: '700'
    lineHeight: '1'
    letterSpacing: 0.1em
rounded:
  sm: 0.125rem
  DEFAULT: 0.25rem
  md: 0.375rem
  lg: 0.5rem
  xl: 0.75rem
  full: 9999px
spacing:
  unit: 8px
  container-max: 1440px
  gutter: 32px
  margin-edge: 64px
  section-gap: 120px
---

## Brand & Style

The brand personality is curated, prestigious, and intellectually stimulating. It positions film not just as entertainment, but as an architectural and cultural art form. The UI should evoke the feeling of walking through a high-end gallery or flipping through a premium design publication—uncluttered, intentional, and high-contrast.

This design system utilizes a **Minimalist** style with a focus on editorial composition. By prioritizing generous whitespace and a restricted color palette, the system allows the cinematic imagery to act as the primary visual driver. The aesthetic is defined by crisp lines, structural alignment, and a "less is more" approach that emphasizes the premium nature of the content.

## Colors

The palette is rooted in a "High-Key" light mode configuration. Pure White (#FFFFFF) serves as the primary canvas to ensure an airy and expansive feel. Depth is achieved through a hierarchy of very light grays rather than shadows, maintaining a clean, architectural look.

*   **Primary (Cinema Red):** Reserved strictly for calls to action, active states, and critical highlights. It provides a sharp, energetic contrast to the neutral base.
*   **Neutral Palette:** Uses a range of cool grays to define UI boundaries and secondary containers without breaking the minimalist flow.
*   **Typography & Accents:** A deep Charcoal (#1A1A1A) is used for primary text to ensure maximum readability and a premium, ink-on-paper feel.

## Typography

This design system employs **Inter** for its clinical precision and exceptional legibility across all weights. The typographic scale is influenced by modern editorial design, featuring dramatic contrasts between display headlines and body copy.

Headlines should be set with tight letter-spacing to create a "blocky," authoritative appearance. Labels and metadata should utilize uppercase styling with increased letter-spacing to mimic the footers of architectural blueprints or magazine mastheads. Paragraph text remains open and airy to ensure a comfortable reading experience for film synopses and reviews.

## Layout & Spacing

The layout follows a **Fixed Grid** model inspired by print media. A 12-column grid provides the structural foundation, but the defining characteristic is the use of "Safe Zones"—extremely wide outer margins (64px+) that frame the content like a mat around a photograph.

Spacing is aggressive and intentional. Significant vertical gaps (Section Gaps) are used to separate different content types, preventing the UI from feeling crowded. Elements should be grouped using a strict 8px base unit, but the relationship between major components should favor "over-spacing" to maintain the airy, high-end aesthetic.

## Elevation & Depth

This design system rejects heavy drop shadows in favor of **Tonal Layering** and **Low-Contrast Outlines**. Depth is communicated through subtle shifts in background value rather than physical distance.

*   **Tier 1 (Surface):** Pure White (#FFFFFF) for the main background.
*   **Tier 2 (Container):** Ghost White (#F8F9FA) for secondary content areas like sidebars or card backgrounds.
*   **Tier 3 (Overlay):** Soft, 1px borders in Light Gray (#E9ECEF) are used to define interactive elements.
*   **Floating Elements:** Only high-priority modals or dropdowns may use a very soft, highly diffused ambient shadow (0px 20px 40px rgba(0,0,0,0.05)) to suggest a gentle lift without breaking the flat, architectural plane.

## Shapes

The shape language is precise and geometric. A **Soft (0.25rem)** roundedness is applied to buttons and input fields to make them feel modern and tactile, while larger containers like film posters and hero banners should remain **Sharp (0px)** to maintain the structural, magazine-like aesthetic. 

The juxtaposition of slightly softened interactive elements against sharp-edged imagery creates a sophisticated visual tension that feels custom and high-end.

## Components

Components within this design system are designed to be "invisible" until needed, allowing the film content to take center stage.

*   **Buttons:** Primary buttons use a solid 'Cinema Red' fill with white text. Secondary buttons use a transparent background with a 1px charcoal border (Ghost style). All buttons use bold, uppercase labels.
*   **Cards:** Film cards are borderless with sharp corners. Information (title, year) appears only on hover or is tucked neatly below the image in a minimalist label style.
*   **Input Fields:** Use a minimalist "Bottom-Border" only style for search and forms to reduce visual noise, turning Red only on focus.
*   **Chips/Tags:** Small, rectangular shapes with Ghost White backgrounds and tiny, wide-spaced uppercase text.
*   **Navigation:** A persistent but slim top bar with high-transparency blur, using thin weights for links to maintain the airy feel.
*   **Progress Indicators:** Thin, 2px red lines for video progress and scroll indicators to maintain a delicate, technical appearance.