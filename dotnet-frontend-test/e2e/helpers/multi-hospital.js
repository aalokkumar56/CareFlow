const fs = require("fs");
const path = require("path");
const { apiLogin, searchPatients } = require("./api");

const CACHE_PATH = path.join(__dirname, ".e2e-multi-hospital.json");

/** Mirrors MultiHospitalE2eSeeder.Hospitals in the backend. */
const HOSPITALS = [
  {
    key: "althan",
    slug: "care-cure-althan",
    name: "Care & Cure Althan",
    adminEmail: "admin@care-cure-althan.e2e.cureflow.test",
    password: "Test@12345",
    exclusivePatient: "Althan Exclusive Patient",
    patientPhone: "919100000001",
    departments: ["General Medicine", "Cardiology", "Orthopedics"],
  },
  {
    key: "surat",
    slug: "city-hospital-surat",
    name: "City Hospital Surat",
    adminEmail: "admin@city-hospital-surat.e2e.cureflow.test",
    password: "Test@12345",
    exclusivePatient: "Surat Exclusive Patient",
    patientPhone: "919100000002",
    departments: ["Orthopedics", "Dermatology", "ENT"],
  },
  {
    key: "pune",
    slug: "metro-clinic-pune",
    name: "Metro Clinic Pune",
    adminEmail: "admin@metro-clinic-pune.e2e.cureflow.test",
    password: "Test@12345",
    exclusivePatient: "Pune Exclusive Patient",
    patientPhone: "919100000003",
    departments: ["Pediatrics", "General Medicine", "Gynecology"],
  },
];

/** Phase 1 aliases — Hospital A/B/C map to Althan/Surat/Pune seeder tenants. */
const HOSPITAL_A = HOSPITALS[0];
const HOSPITAL_B = HOSPITALS[1];
const HOSPITAL_C = HOSPITALS[2];

/** @param {string} key */
function getHospitalByKey(key) {
  const hospital = HOSPITALS.find((h) => h.key === key);
  if (!hospital) throw new Error(`Unknown hospital key: ${key}`);
  return hospital;
}

/** @returns {Promise<Record<string, object>>} */
async function ensureMultiHospitals({ staggerMs = 2000 } = {}) {
  const result = {};

  for (let i = 0; i < HOSPITALS.length; i += 1) {
    const hospital = HOSPITALS[i];
    if (i > 0 && staggerMs > 0) {
      await new Promise((resolve) => setTimeout(resolve, staggerMs));
    }

    let accessToken;
    let user;
    try {
      const auth = await apiLogin(hospital.adminEmail, hospital.password);
      accessToken = auth.accessToken;
      user = auth.user;
    } catch (err) {
      throw new Error(
        `Multi-hospital E2E: cannot login as ${hospital.adminEmail}. ` +
          `Restart API so MultiHospitalE2eSeeder runs. ${err.message}`,
      );
    }

    let patientId = loadMultiHospitalCache()?.[hospital.key]?.patientId;
    if (!patientId) {
      const matches = await searchPatients(accessToken, hospital.exclusivePatient);
      const patient = matches.find((p) => p.name === hospital.exclusivePatient);
      if (patient) patientId = patient.id;
    }

    result[hospital.key] = {
      ...hospital,
      accessToken,
      user,
      patientId,
    };
  }

  fs.mkdirSync(path.dirname(CACHE_PATH), { recursive: true });
  fs.writeFileSync(
    CACHE_PATH,
    JSON.stringify(
      Object.fromEntries(
        Object.entries(result).map(([key, h]) => [
          key,
          {
            key: h.key,
            slug: h.slug,
            name: h.name,
            adminEmail: h.adminEmail,
            password: h.password,
            exclusivePatient: h.exclusivePatient,
            patientPhone: h.patientPhone,
            departments: h.departments,
            patientId: h.patientId,
            tenantId: h.user?.tenant_id || h.user?.tenantId,
          },
        ]),
      ),
      null,
      2,
    ),
  );

  return result;
}

/** @returns {Record<string, object> | null} */
function loadMultiHospitalCache() {
  try {
    return JSON.parse(fs.readFileSync(CACHE_PATH, "utf8"));
  } catch {
    return null;
  }
}

/** @param {string} token @param {string} patientId */
async function getPatientOrNull(token, patientId) {
  const res = await fetch(
    `${process.env.PLAYWRIGHT_API_URL || "http://localhost:5180"}/api/patients/${patientId}`,
    { headers: { Authorization: `Bearer ${token}` } },
  );
  if (res.status === 404) return null;
  if (!res.ok) {
    throw new Error(`GET patient ${patientId} failed: ${res.status} ${await res.text()}`);
  }
  return res.json();
}

/** @param {string} token @param {string} query */
async function patientSearchCount(token, query) {
  const items = await searchPatients(token, query);
  return items.length;
}

module.exports = {
  HOSPITALS,
  HOSPITAL_A,
  HOSPITAL_B,
  HOSPITAL_C,
  CACHE_PATH,
  ensureMultiHospitals,
  loadMultiHospitalCache,
  getHospitalByKey,
  getPatientOrNull,
  patientSearchCount,
};
