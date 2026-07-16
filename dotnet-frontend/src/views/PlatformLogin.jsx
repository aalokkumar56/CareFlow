import React, { useState } from "react";
import { useNavigate } from "@/lib/navigation";
import { platformLogin } from "@/lib/platformAuth";
import { normalizeApiError } from "@/lib/api";
import { EnvelopeSimple, Lock } from "@phosphor-icons/react";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { toast } from "sonner";
import { Toaster } from "@/components/ui/sonner";
import CureFlowMark from "@/components/brand/CureFlowMark";

const PlatformLogin = () => {
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [loading, setLoading] = useState(false);

  const submit = async (e) => {
    e.preventDefault();
    setLoading(true);
    try {
      await platformLogin(email, password);
      toast.success("Platform access granted");
      navigate("/platform/tenants");
    } catch (err) {
      toast.error(normalizeApiError(err, "Platform login failed"));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-background flex items-center justify-center p-6" data-testid="platform-login-page">
      <Toaster position="top-center" richColors />
      <div className="w-full max-w-md glass-card p-8">
        <CureFlowMark variant="platform" className="mb-6" />
        <h1 className="font-heading text-xl font-semibold text-center mb-1" data-testid="platform-login-title">
          CureFlow Platform
        </h1>
        <p className="text-sm text-muted-foreground text-center mb-6">
          Ops sign in — approve hospitals, manage tenant lifecycle
        </p>
        <form onSubmit={submit} className="space-y-3" data-testid="platform-login-form">
          <div>
            <label className="text-xs font-medium text-muted-foreground block mb-1">Email</label>
            <div className="relative">
              <EnvelopeSimple className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
              <Input
                data-testid="platform-login-email"
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                required
                className="pl-10"
                placeholder="ops@cureflow.in"
              />
            </div>
          </div>
          <div>
            <label className="text-xs font-medium text-muted-foreground block mb-1">Password</label>
            <div className="relative">
              <Lock className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
              <Input
                data-testid="platform-login-password"
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
                className="pl-10"
              />
            </div>
          </div>
          <Button type="submit" className="w-full" disabled={loading} data-testid="platform-login-submit">
            {loading ? "Signing in..." : "Sign in"}
          </Button>
        </form>
      </div>
    </div>
  );
};

export default PlatformLogin;
