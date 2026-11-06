# RADAR

Read docs/DEV_HANDOFF.md and docs/DEV_SYNC.md before work.
Locate the repository with git rev-parse. Paths and drive letters differ by computer.
Use the same branch name across locations. Never infer unpublished work from GitHub.
Run tools/Sync-RadarWorkspace.ps1 -Action Start before starting an established branch.
If the gate fails, inspect the reason and preserve all local work. A newly created
development branch may be edited and published under the owner's standing authorization.

The owner permanently authorizes commits and safe pushes on development branches
in both locations. Before edits inspect status and fetch origin. After each unit,
run relevant tests, review diffs and new files, scan for secrets, update DEV_HANDOFF,
commit descriptively and push the same development branch. Never work directly on master.
Preserve and reconcile divergent work without discarding it or force pushing.
Do not merge to master, deploy, change production, execute production migrations or SQL 57/58,
start the real Listener, or send WhatsApp without explicit authorization.
Do not implement poison-message recovery again. Import the original work when available.
Publish that work only after its real files and integrity have been verified.
Do not reset, clean, force checkout, force push, or automatically stash user changes.
Keep credentials, production data, publish profiles and rollback packages outside Git.
The verified GitHub repository checua/maps4 is public. Treat secret scan findings as blockers.
Never print credential values. Secret scanning is heuristic and requires human review.
Update handoff documentation with changes, tests, limitations and next steps.
