import React, { useState } from "react";
import { Link, useNavigate } from "@/lib/navigation";
import { api, normalizeApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import CureFlowMark from "@/components/brand/CureFlowMark";
import { EnvelopeSimple, Lock, Buildings, User, Phone } from "@phosphor-icons/react";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { toast } from "sonner";
import { Toaster } from "@/components/ui/sonner";

const LOGIN_BG = "https://static.prod-images.emergentagent.com/jobs/6650abee-e9ca-4809-bc95-2083338f98c5/images/cbc93e151dd02be7334cfb1572ee6047c7c9fd89f8c76cb7af56e3c53dcb38e1.png";

const Signup = () => {
  const navigate = useNavigate();
  const { login, postLoginRoute } = useAuth();
  const [hospitalName, setHospitalName] = useState("");
  const [adminName, setAdminName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [phone, setPhone] = useState("");
  const [loading, setLoading] = useState(false);
  const [bgFailed, setBgFailed] = useState(false);

  const submit = async (e) => {
    e.preventDefault();
    setLoading(true);
    try {
      await api.post("/auth/register-tenant", {
        hospital_name: hospitalName,
        admin_name: adminName,
        admin_email: email,
        admin_password: password,
        phone: phone || undefined,
      });
      const { tenant } = await login(email, password);
      toast.success("Hospital registered! Awaiting CureFlow approval.");
      navigate(postLoginRoute(tenant));
    } catch (err) {
      toast.error(normalizeApiError(err, "Registration failed"));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen relative flex items-center justify-center p-4 sm:p-8">
      {!bgFailed && (
        <img
          src={LOGIN_BG}
          alt=""
          className="absolute inset-0 w-full h-full object-cover"
          onError={() => setBgFailed(true)}
        />
      )}
      <div className="absolute inset-0 bg-gradient-to-br from-[#022C22]/75 via-[#064E3B]/55 to-[#022C22]/70" />

      <div className="relative z-10 w-full max-w-[480px]">
        <div className="glass-card bg-white/15 backdrop-blur-2xl border-white/25 shadow-2xl p-6 sm:p-8">
          <div className="flex flex-col items-center text-center mb-6">
            <CureFlowMark light className="mb-3" />
            <h1 className="font-heading text-xl font-semibold text-white">Register your hospital</h1>
            <p className="text-white/70 text-[13px] mt-1">Register your hospital on CureFlow — 14-day trial, no credit card</p>
          </div>

          <form onSubmit={submit} className="space-y-3" data-testid="signup-form">
            <div>
              <label className="text-[12px] font-medium text-white/80 block mb-1.5">Hospital name</label>
              <div className="relative">
                <Buildings className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-white/50" />
                <Input
                  data-testid="signup-hospital-name"
                  value={hospitalName}
                  onChange={(e) => setHospitalName(e.target.value)}
                  required
                  placeholder="City Hospital"
                  className="pl-10 rounded-xl bg-white/10 border-white/20 text-white placeholder:text-white/40 h-11"
                />
              </div>
            </div>
            <div>
              <label className="text-[12px] font-medium text-white/80 block mb-1.5">Admin name</label>
              <div className="relative">
                <User className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-white/50" />
                <Input
                  data-testid="signup-admin-name"
                  value={adminName}
                  onChange={(e) => setAdminName(e.target.value)}
                  required
                  placeholder="Dr. Admin"
                  className="pl-10 rounded-xl bg-white/10 border-white/20 text-white placeholder:text-white/40 h-11"
                />
              </div>
            </div>
            <div>
              <label className="text-[12px] font-medium text-white/80 block mb-1.5">Admin email</label>
              <div className="relative">
                <EnvelopeSimple className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-white/50" />
                <Input
                  data-testid="signup-email"
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  required
                  placeholder="admin@hospital.com"
                  className="pl-10 rounded-xl bg-white/10 border-white/20 text-white placeholder:text-white/40 h-11"
                />
              </div>
            </div>
            <div>
              <label className="text-[12px] font-medium text-white/80 block mb-1.5">Password</label>
              <div className="relative">
                <Lock className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-white/50" />
                <Input
                  data-testid="signup-password"
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  required
                  minLength={8}
                  placeholder="Min 8 characters"
                  className="pl-10 rounded-xl bg-white/10 border-white/20 text-white placeholder:text-white/40 h-11"
                />
              </div>
            </div>
            <div>
              <label className="text-[12px] font-medium text-white/80 block mb-1.5">Phone (optional)</label>
              <div className="relative">
                <Phone className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-white/50" />
                <Input
                  data-testid="signup-phone"
                  value={phone}
                  onChange={(e) => setPhone(e.target.value)}
                  placeholder="919876543210"
                  className="pl-10 rounded-xl bg-white/10 border-white/20 text-white placeholder:text-white/40 h-11"
                />
              </div>
            </div>

            <Button
              data-testid="signup-submit"
              type="submit"
              disabled={loading}
              className="w-full h-11 rounded-xl bg-gradient-to-r from-emerald-400 to-teal-600 hover:from-emerald-500 hover:to-teal-700 text-white font-medium border-0 mt-2"
            >
              {loading ? "Creating hospital..." : "Create hospital account"}
            </Button>
          </form>

          <p className="text-center text-[12px] text-white/70 mt-5">
            Already have an account?{" "}
            <Link to="/login" className="text-emerald-300 hover:underline" data-testid="signup-login-link">
              Sign in
            </Link>
          </p>
        </div>
      </div>
      <Toaster position="top-right" />
    </div>
  );
};

export default Signup;
