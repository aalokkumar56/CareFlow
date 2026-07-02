export const BACKEND_URL = "http://localhost:5180";
export const API_BASE = `${BACKEND_URL}/api`;

export const api = {
  get: jest.fn(() => Promise.resolve({ data: {} })),
  post: jest.fn(() => Promise.resolve({ data: {} })),
};

export const normalizeApiError = (_error, fallback = "Request failed") => fallback;
export const formatPhone = (phone) => phone ?? "";
export const fetchAuthorizedBlob = jest.fn(() => Promise.resolve(new Blob()));
export const resolveProtectedMediaPath = () => null;
