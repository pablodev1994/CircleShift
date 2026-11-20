/** Community roles from docs/product/personas.md. */
export const ROLES = ['member', 'coordinator', 'moderator', 'admin'] as const;

export type Role = (typeof ROLES)[number];

export const ROLE_LABELS: Readonly<Record<Role, string>> = {
  member: 'Member',
  coordinator: 'Coordinator',
  moderator: 'Moderator',
  admin: 'Platform admin',
};

export function isRole(value: string): value is Role {
  return (ROLES as readonly string[]).includes(value);
}
