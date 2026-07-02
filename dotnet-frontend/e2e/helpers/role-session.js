const { apiLogin } = require("./api");
const { ensureTestUsers } = require("./test-users");
const { getPermissionsForUser } = require("../../src/lib/permissions");
const { ALL_ROLES_MATRIX } = require("./routes");

/** @type {Record<string, { email: string, password: string, permissions: string[] }> | null} */
let _roleCache = null;
let _stamp = null;

/** Provision E2E users once and cache JWT-derived permissions per role. */
async function getRoleSessions(stamp = Date.now()) {
  if (_roleCache) return _roleCache;
  _stamp = stamp;

  await ensureTestUsers(_stamp);
  const sessions = {};

  for (const role of ALL_ROLES_MATRIX) {
    const email = role === "admin"
      ? "admin@cureflow.in"
      : `e2e.${role}.${_stamp}@cureflow.test`;
    const password = role === "admin" ? "admin123" : require("./test-users").E2E_PASSWORD;

    const { user } = await apiLogin(email, password);
    sessions[role] = {
      email,
      password,
      permissions: getPermissionsForUser(user),
    };
  }

  _roleCache = sessions;
  return sessions;
}

function clearRoleSessionCache() {
  _roleCache = null;
}

module.exports = { getRoleSessions, clearRoleSessionCache };
