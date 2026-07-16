import React from "react";
import { Link } from "@/lib/navigation";
import { CheckCircle } from "@phosphor-icons/react";
import { Button } from "@/components/ui/button";
import CureFlowMark from "@/components/brand/CureFlowMark";

const RegistrationReceived = () => (
  <div className="min-h-screen bg-background flex items-center justify-center p-6" data-testid="registration-received-page">
    <div className="max-w-md w-full glass-card p-8 text-center">
      <div className="flex justify-center mb-4">
        <CheckCircle className="w-12 h-12 text-emerald-500" weight="duotone" />
      </div>
      <CureFlowMark className="mb-4" />
      <h1 className="font-heading text-xl font-semibold mb-2" data-testid="registration-received-title">
        Registration received
      </h1>
      <p className="text-sm text-muted-foreground mb-6">
        Thank you for registering your hospital on CureFlow. Our team will review your application and notify you
        when your account is approved.
      </p>
      <Button asChild data-testid="registration-received-login">
        <Link href="/login">Go to sign in</Link>
      </Button>
    </div>
  </div>
);

export default RegistrationReceived;
