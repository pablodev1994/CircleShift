# Personas

All personas and names are fictional. Every person in CircleShift is an adult (18+).

## Member — Laura, 29, junior front-end developer

- **Offers:** React and TypeScript mentoring, 45-minute remote sessions.
- **Wants:** help with portrait photography for her portfolio.
- **Goals:** find someone whose skills complement hers without paying, at times that fit her evenings in `America/Bogota`.
- **Frustrations:** generic social feeds; long back-and-forth to agree on a time.
- **Needs from CircleShift:** a match list that explains *why* someone fits, clear local times, and a credit balance she can trust.

## Member — Tomás, 41, hobby photographer and cyclist

- **Offers:** portrait photography basics, bike maintenance (in person, coarse area only).
- **Wants:** conversational English practice.
- **Goals:** trade what he knows for something he needs; meet people nearby without sharing his address.
- **Needs from CircleShift:** privacy by default, the ability to block someone immediately, and in-person sessions limited to people he has accepted.

## Coordinator — Marisol, 35, runs a weekly language circle

- Hosts a public "Spanish ↔ English conversation" circle with a capped number of seats per event.
- Approves sessions that happen inside her circle and sees aggregate attendance, not private member data.
- **Needs:** event capacity that cannot be oversold, and a simple member list for her own circle only.

## Moderator — Andrés, 33, volunteer moderator

- Reviews reported posts and messages and decides approve, warn, hide or suspend.
- Sees model-suggested flags with evidence and a confidence score, but always makes and signs the decision.
- **Needs:** a queue ordered by user reports first, the exact text that triggered a flag, and the full history when an appeal arrives.

## Platform admin — Camila, 38

- Manages community policy versions, skill catalog entries and role assignments.
- Cannot read private conversations unless a report makes them part of a moderation case.
- **Needs:** an audit trail for every role change and policy update.

## Permission summary

| Capability | Member | Coordinator | Moderator | Admin |
|---|---|---|---|---|
| Create profile, posts, exchanges | ✅ | ✅ | ✅ | ✅ |
| View own wallet and history | ✅ | ✅ | ✅ | ✅ |
| Create circles and events | — | ✅ | — | ✅ |
| Approve sessions in own circle | — | ✅ | — | — |
| Review reports and decide cases | — | — | ✅ | ✅ |
| Decide appeals | — | — | ✅ (not own case) | ✅ |
| Manage policies, catalog, roles | — | — | — | ✅ |

The server enforces every row of this table. Angular route guards only hide navigation the user cannot use.
