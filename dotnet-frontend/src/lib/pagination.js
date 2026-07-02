/**
 * Normalize a paged API response into { items, total, page, pageSize }.
 * Accepts axios response, raw payload, legacy flat arrays, or paged envelopes.
 */
export function unwrapPaged(response) {
  const data = response?.data ?? response;

  if (Array.isArray(data)) {
    return { items: data, total: data.length, page: 1, pageSize: data.length || 1 };
  }

  const items = data?.items ?? [];
  return {
    items,
    total: data?.total ?? items.length,
    page: data?.page ?? 1,
    pageSize: data?.page_size ?? data?.pageSize ?? (items.length || 1),
  };
}

export function buildPageQuery(params = {}) {
  const qs = new URLSearchParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== "" && value !== "all") {
      qs.append(key, String(value));
    }
  });
  return qs.toString();
}
