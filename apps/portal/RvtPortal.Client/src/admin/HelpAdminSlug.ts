// File summary: Shared Help/FAQ admin slug helpers.
// Major updates:
// - 2026-06-29 pending Added linear Help CMS slug generation for Sonar reliability.

// Function summary: Builds a URL-safe Help CMS slug from a display value.
export function slugify(value: string) {
  const slug: string[] = [];
  let hasPendingSeparator = false;

  for (const char of value.trim().toLowerCase()) {
    if (isSlugCharacter(char)) {
      if (hasPendingSeparator && slug.length > 0) {
        slug.push('-');
      }
      slug.push(char);
      hasPendingSeparator = false;
      continue;
    }

    if (slug.length > 0) {
      hasPendingSeparator = true;
    }
  }

  return slug.join('');
}

function isSlugCharacter(char: string) {
  return (char >= 'a' && char <= 'z') || (char >= '0' && char <= '9');
}
