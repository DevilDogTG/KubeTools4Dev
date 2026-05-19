# Plan: Left Sidebar Navigation

**Status:** active
**Created:** 2026-05-18
**Branch:** feat/left-sidebar-navigation

## Goal
Replace the top TabControl with a collapsible left sidebar that shows icon-only when collapsed and icon+label when expanded, with animated width transition.

## Checklist

- [x] Replace top TabControl with left sidebar layout in `MainWindow.axaml`
- [x] Add `ToggleSidebarCommand`, `IsSidebarExpanded`, `SidebarWidth` to `MainViewModel.cs`
- [x] Add `SelectedNavIndex`-driven visibility (`IsPodsVisible`, `IsServicesVisible`, `IsSettingsVisible`)
- [x] Add Material Icons (LayersTriple=Pods, ShareVariant=Services, Cog=Settings)
- [x] Add animated width transition (0.2s `DoubleTransition`)
- [x] Add sidebar styles in `App.axaml` (SidebarNav, SidebarToggle, SidebarNavList, SidebarNavItem + hover/selected states)
- [x] Fix icon centering when collapsed — `HorizontalContentAlignment="Stretch"` + `ColumnDefinitions="20,*"` + `HorizontalAlignment="Center"` on each icon
- [x] Add `ToolTip.Tip` to each nav ListBoxItem (Pods / Services / Settings)
- [ ] Confirm feature complete with user — ready to open PR?

## Progress Log

### 2026-05-18 — Session resumed
- Two commits already on branch: sidebar layout + styles in first commit, collapsible toggle in second.
- `MainWindow.axaml`: DockPanel sidebar with toggle button + ListBox nav; content area with 3 visibility-gated panels.
- `MainViewModel.cs`: Full observable state (expand/collapse, width, nav index, visibility flags).
- `App.axaml`: Styles for all sidebar classes including hover/selected/selected+hover states.
- Next: confirm with user that feature is complete and determine if any polish is needed before opening PR.
