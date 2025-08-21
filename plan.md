# UI Consolidation Plan

## Phase 1: Update Home View
1. Remove navigation bar from _Layout.cshtml
2. Center main heading, remove tagline
3. Combine Docker/Container status cards into single card
4. Add container details table below status cards
5. Add DNS Server card with admin URL & credentials
6. Add Proxy Manager card with admin URL & credentials

## Phase 2: Backend Updates
1. Update HomeController Index action to fetch container details
2. Update HomeViewModel to include container status data
3. Add auto-refresh (30 seconds) to Home view

## Phase 3: Cleanup
1. Remove Status action from HomeController
2. Delete Status.cshtml view
3. Delete StatusViewModel
4. Remove any Status-related navigation links
5. Test consolidated single-view application