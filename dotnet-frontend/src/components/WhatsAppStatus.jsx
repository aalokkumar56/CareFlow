import React, { useState, useEffect } from "react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { api } from "@/lib/api";
import { ArrowsClockwiseIcon, CircleNotchIcon } from "@phosphor-icons/react";
import { Button } from "@/components/ui/button";

export const WhatsAppStatus = () => {
  const [status, setStatus] = useState(null);
  const [loading, setLoading] = useState(false);

  const checkStatus = async () => {
    setLoading(true);
    try {
      const response = await api.get("/whatsapp/health");
      setStatus(response.data);
    } catch (error) {
      setStatus({ status: "error", message: "Failed to check status" });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    checkStatus();
  }, []);

  const getStatusColor = () => {
    if (!status) return "bg-gray-100";
    return status.configured ? "bg-green-100" : "bg-red-100";
  };

  const getStatusText = () => {
    if (!status) return "Checking...";
    return status.configured ? "✓ Configured" : "✗ Not Configured";
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center justify-between">
          <span>WhatsApp Integration</span>
          <Button
            variant="ghost"
            size="sm"
            onClick={checkStatus}
            disabled={loading}
          >
            {loading ? <CircleNotchIcon size={16} className="animate-spin" /> : <ArrowsClockwiseIcon size={16} />}
          </Button>
        </CardTitle>
        <CardDescription>API Service Status</CardDescription>
      </CardHeader>
      <CardContent>
        <div className={`p-4 rounded ${getStatusColor()}`}>
          <p className="text-sm font-medium">{getStatusText()}</p>
          {status?.status === "error" && (
            <p className="text-xs text-red-600 mt-2">
              Make sure to add your WhatsApp API token to appsettings.json
            </p>
          )}
        </div>
      </CardContent>
    </Card>
  );
};

export default WhatsAppStatus;
