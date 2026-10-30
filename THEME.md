# THEME

This project uses the Shinra house interface color theme, Shinra Aero Glass (Dark).

The master definition, the token palette, and the per-framework application
instructions live in the house technical-writing repository:

```
C:\Users\scook\Projects\technicalWriting\SHINRA_THEME.md
```

Consult the master guide before changing any color value. This project follows the
master and never redefines a token locally.

Implementation in this project (Avalonia, see master Section 4.3):

- `src/ShinraShot.App/Theming/Palette.cs` carries the code-side token brushes.
- `src/ShinraShot.App/App.axaml` sets the dark theme variant, the accent color, and
  the window background.
