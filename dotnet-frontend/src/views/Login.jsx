import React, { useState } from "react";
import { useNavigate } from "@/lib/navigation";
import { useAuth } from "@/lib/auth";
import { normalizeApiError } from "@/lib/api";
import { Sparkle, EnvelopeSimple, Lock } from "@phosphor-icons/react";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { toast } from "sonner";
import { Toaster } from "@/components/ui/sonner";

const LOGIN_BG = "https://static.prod-images.emergentagent.com/jobs/6650abee-e9ca-4809-bc95-2083338f98c5/images/cbc93e151dd02be7334cfb1572ee6047c7c9fd89f8c76cb7af56e3c53dcb38e1.png";

const Login = () => {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [loading, setLoading] = useState(false);
  const [bgFailed, setBgFailed] = useState(false);

  const submit = async (e) => {
    e.preventDefault();
    setLoading(true);
    try {
      await login(email, password);
      toast.success("Welcome back!");
      navigate("/");
    } catch (err) {
      toast.error(normalizeApiError(err, "Login failed"));
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

      <div className="relative z-10 w-full max-w-[440px]">
        <p className="text-center text-white/90 text-sm sm:text-base mb-6 drop-shadow">
          Run your hospital like{" "}
          <span className="text-emerald-300 font-medium">a modern growth company</span>
        </p>

        <div className="glass-card bg-white/15 backdrop-blur-2xl border-white/25 shadow-2xl p-6 sm:p-8">
          <div className="flex flex-col items-center text-center mb-6">
            <div className="flex items-center gap-2 mb-2">
              <Sparkle weight="fill" className="w-6 h-6 text-emerald-300" />
              <span className="font-heading text-2xl font-semibold text-white tracking-tight lowercase">
                cure&amp;care
              </span>
            </div>
            <h1 className="font-heading text-xl font-semibold text-white">Welcome back</h1>
            <p className="text-white/70 text-[13px] mt-1">Sign in to continue to your account</p>
          </div>

          <form onSubmit={submit} className="space-y-4" data-testid="login-form">
            <div>
              <label className="text-[12px] font-medium text-white/80 block mb-1.5">Email address</label>
              <div className="relative">
                <EnvelopeSimple className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-white/50" />
                <Input
                  data-testid="login-email"
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  required
                  placeholder="Enter your email"
                  className="pl-10 rounded-xl bg-white/10 border-white/20 text-white placeholder:text-white/40 h-11"
                />
              </div>
            </div>
            <div>
              <label className="text-[12px] font-medium text-white/80 block mb-1.5">Password</label>
              <div className="relative">
                <Lock className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-white/50" />
                <Input
                  data-testid="login-password"
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  required
                  placeholder="Enter your password"
                  className="pl-10 rounded-xl bg-white/10 border-white/20 text-white placeholder:text-white/40 h-11"
                />
              </div>
            </div>

            <Button
              data-testid="login-submit"
              type="submit"
              disabled={loading}
              className="w-full h-11 rounded-xl bg-gradient-to-r from-emerald-400 to-teal-600 hover:from-emerald-500 hover:to-teal-700 text-white font-medium border-0"
            >
              {loading ? "Signing in..." : "Sign in"}
            </Button>
          </form>

          <p className="text-center text-[11px] text-emerald-200/80 mt-6">
            Secure · Reliable · Built for modern healthcare
          </p>
        </div>
      </div>
      <Toaster position="top-right" />
    </div>
  );
};

export default Login;
