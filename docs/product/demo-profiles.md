# Demo profiles and skill catalog

The demo data lets anyone run CircleShift locally and see realistic exchanges
without touching personal data. Everything here is fictional.

## Rules for demo data

- **Adults only.** Every demo member has a recorded 18+ confirmation, the same
  as real members. No demo profile describes or implies a minor.
- **Fictional people.** First name plus an initial; no surnames, photos, email
  addresses, phone numbers or social handles.
- **No precise location.** Only an IANA time zone. In-person preferences,
  when they arrive, use a coarse area, never an address.
- **No money.** Profiles trade simulated time credits only.
- **Development only.** Demo members are refused outside the `Development`
  environment. The skill catalog is seeded everywhere.

## Demo members

The first five match the [personas](personas.md). The extra members give the
matcher more than one candidate per skill.

| Handle | Display name | Time zone | Role in the demo |
|---|---|---|---|
| `laura` | Laura M. | `America/Bogota` | Member: React/TypeScript mentor, wants portrait photography help |
| `tomas` | Tomás R. | `Europe/Madrid` | Member: photographer and cyclist, wants English conversation |
| `marisol` | Marisol V. | `America/Mexico_City` | Coordinator: runs a Spanish ↔ English conversation circle |
| `andres` | Andrés P. | `America/Lima` | Moderator: volunteer moderator |
| `camila` | Camila S. | `Europe/Lisbon` | Platform admin: policies and skill catalog |
| `priya` | Priya K. | `Asia/Kolkata` | Member: Python tutor, wants watercolor tips |
| `jonas` | Jonas W. | `Europe/Berlin` | Member: bike mechanic, wants Spanish conversation |
| `amara` | Amara O. | `Africa/Lagos` | Member: interview coach, wants guitar basics |

The time zones cover offsets from UTC−6 to UTC+5:30 and both hemispheres'
daylight-saving rules, so availability work can be tested across real changes.

Marisol, Andrés and Camila get their persona role (coordinator, moderator,
admin) as a verified seed grant. Every demo profile starts **hidden**, like a
new member's. Profile details, offers and requests are added to the seed as
those features are built.

Sign-in accounts (`<handle>@demo.circleshift.example`, a reserved example
domain) are created only when `CIRCLESHIFT_DEMO_PASSWORD` is set when the seed
runs; all demo accounts share that password. It must be 12–128 characters and
is never stored in the repository.

## Canonical skill catalog

24 skills in six categories. Slugs are lowercase words joined by single
hyphens; the database repeats the rule in a CHECK constraint.

| Category | Skills (slug) |
|---|---|
| Languages | `english-conversation`, `spanish-conversation`, `portuguese-basics`, `german-basics` |
| Technology | `react`, `typescript`, `python-basics`, `spreadsheets` |
| Creative | `portrait-photography`, `photo-editing`, `guitar-basics`, `watercolor-painting` |
| Home | `bike-repair`, `gardening`, `home-cooking`, `sewing-repairs` |
| Career | `resume-review`, `interview-practice`, `public-speaking`, `personal-budgeting` |
| Wellbeing | `yoga-basics`, `running-coaching`, `meditation`, `meal-planning` |

Catalog entries are never renamed or deleted in place: offers, requests and
match explanations refer to them. New skills are added with new slugs.

## Stable ids

Seed rows use name-based (RFC 9562 version 5) ids derived from the slug or
handle, so every fresh database gives `bike-repair` and `laura` the same ids.
Fixtures and links can rely on them.

## Integrity checks

`services/api/tests/CircleShift.Tests/Seeding` checks that:

- catalog ids, slugs and names are unique, every category is used, and every
  entry passes the same domain rules as admin-created skills;
- demo members are unique, adult-confirmed, use valid IANA zones, include all
  five personas and hold no contact details;
- seeding twice adds nothing, fills in only missing catalog entries, and stops
  without writing anything when a catalog slug already belongs to another id.

See [development setup](../setup/dev-setup.md#seed-demo-data) for the seed command.
