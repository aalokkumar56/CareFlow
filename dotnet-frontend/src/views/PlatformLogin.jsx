import React, { useEffect, useState } from "react";
import { useNavigate } from "@/lib/navigation";
import {
  fetchPlatformSetupStatus,
  platformBootstrap,
  platformLogin,
} from "@/lib/platformAuth";
import { normalizeApiError } from "@/lib/api";
import { EnvelopeSimple, Lock, User } from "@phosphor-icons/react";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { toast } from "sonner";
import { Toaster } from "@/components/ui/sonner";
import CureFlowMark from "@/components/brand/CureFlowMark";

const PlatformLogin = () => {
  const navigate = useNavigate();
  const [checking, setChecking] = useState(true);
  const [needsSetup, setNeedsSetup] = useState(false);
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    let active = true;
    fetchPlatformSetupStatus()
      .then((status) => {
        if (!active) return;
        setNeedsSetup(!!status.needs_setup);
      })
      .catch(() => {
        if (!active) return;
        setNeedsSetup(false);
      })
      .finally(() => {
        if (active) setChecking(false);
      });
    return () => {
      active = false;
    };
  }, []);

  const submitLogin = async (e) => {
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

  const submitBootstrap = async (e) => {
    e.preventDefault();
    if (password !== confirmPassword) {
      toast.error("Passwords do not match");
      return;
    }
    if (password.length < 8) {
      toast.error("Password must be at least 8 characters");
      return;
    }
    setLoading(true);
    try {
      await platformBootstrap({ name, email, password });
      toast.success("Platform owner created");
      navigate("/platform/tenants");
    } catch (err) {
      toast.error(normalizeApiError(err, "Could not create platform owner"));
    } finally {
      setLoading(false);
    }
  };

  if (checking) {
    return (
      <div className="min-h-screen bg-background flex items-center justify-center p-6" data-testid="platform-login-page">
        <p className="text-sm text-muted-foreground">Checking platform setup…</p>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-background flex items-center justify-center p-6" data-testid="platform-login-page">
      <Toaster position="top-center" richColors />
      <div className="w-full max-w-md glass-card p-8">
        <CureFlowMark variant="platform" className="mb-6" />
        <h1 className="font-heading text-xl font-semibold text-center mb-1" data-testid="platform-login-title">
          CureFlow Platform
        </h1>
        <p className="text-sm text-muted-foreground text-center mb-6">
          {needsSetup
            ? "First-time setup — create the platform owner for this installation"
            : "Ops sign in — approve hospitals, manage tenant lifecycle"}
        </p>

        {needsSetup ? (
          <form onSubmit={submitBootstrap} className="space-y-3" data-testid="platform-bootstrap-form">
            <div>
              <label className="text-xs font-medium text-muted-foreground block mb-1">Full name</label>
              <div className="relative">
                <User className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
                <Input
                  data-testid="platform-bootstrap-name"
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  required
                  minLength={2}
                  className="pl-10"
                  placeholder="Name"
                />
              </div>
            </div>
            <div>
              <label className="text-xs font-medium text-muted-foreground block mb-1">Email</label>
              <div className="relative">
                <EnvelopeSimple className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
                <Input
                  data-testid="platform-bootstrap-email"
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  required
                  className="pl-10"
                  placeholder="Email"
                />
              </div>
            </div>
            <div>
              <label className="text-xs font-medium text-muted-foreground block mb-1">Password</label>
              <div className="relative">
                <Lock className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
                <Input
                  data-testid="platform-bootstrap-password"
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  required
                  minLength={8}
                  className="pl-10"
                />
              </div>
            </div>
            <div>
              <label className="text-xs font-medium text-muted-foreground block mb-1">Confirm password</label>
              <div className="relative">
                <Lock className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
                <Input
                  data-testid="platform-bootstrap-confirm"
                  type="password"
                  value={confirmPassword}
                  onChange={(e) => setConfirmPassword(e.target.value)}
                  required
                  minLength={8}
                  className="pl-10"
                />
              </div>
            </div>
            <Button type="submit" className="w-full" disabled={loading} data-testid="platform-bootstrap-submit">
              {loading ? "Creating owner…" : "Create platform owner"}
            </Button>
          </form>
        ) : (
          <form onSubmit={submitLogin} className="space-y-3" data-testid="platform-login-form">
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
        )}
      </div>
    </div>
  );
};

export default PlatformLogin;
