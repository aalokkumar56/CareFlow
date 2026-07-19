const { apiLogin, createUser } = require("./api");

const E2E_PASSWORD = "TestPass123!";
const ROLES = ["doctor", "reception", "nurse", "marketing", "staff"];

/** @returns {Promise<Record<string, { email: string, password: string, role: string }>>} */
async function ensureTestUsers(stamp = Date.now()) {
  const admin = await apiLogin("admin@cureflow.in", "admin123");
  const users = {
    admin: { email: "admin@cureflow.in", password: "admin123", role: "admin" },
  };

  for (const role of ROLES) {
    const email = `e2e.${role}.${stamp}@cureflow.test`;
    try {
      await createUser(admin.accessToken, {
        name: `E2E ${role}`,
        email,
        password: E2E_PASSWORD,
        role,
      });
    } catch (err) {
      // User may already exist from a prior run — try login
      try {
        await apiLogin(email, E2E_PASSWORD);
      } catch {
        throw new Error(`Failed to create/login e2e user ${role}: ${err.message}`);
      }
    }
    users[role] = { email, password: E2E_PASSWORD, role };
  }

  return users;
}

module.exports = { ensureTestUsers, E2E_PASSWORD, ROLES };
