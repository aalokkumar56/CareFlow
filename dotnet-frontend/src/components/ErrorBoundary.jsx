import React from "react";
import { Link } from "@/lib/navigation";

class ErrorBoundary extends React.Component {
  constructor(props) {
    super(props);
    this.state = { hasError: false, error: null };
  }

  static getDerivedStateFromError(error) {
    return { hasError: true, error };
  }

  componentDidCatch(error, info) {
    console.error("UI error boundary:", error, info);
  }

  render() {
    if (this.state.hasError) {
      return (
        <div className="min-h-screen flex items-center justify-center bg-background p-8">
          <div className="max-w-md text-center space-y-4">
            <h1 className="font-heading text-xl font-semibold text-[#022C22]">Something went wrong</h1>
            <p className="text-[13px] text-text-secondary">
              An unexpected error occurred. Try refreshing the page or return to the dashboard.
            </p>
            <div className="flex items-center justify-center gap-3">
              <button
                type="button"
                onClick={() => window.location.reload()}
                className="text-[13px] px-4 py-2 rounded-sm bg-[#064E3B] text-white hover:bg-[#022C22]"
              >
                Refresh
              </button>
              <Link
                to="/"
                className="text-[13px] px-4 py-2 rounded-sm border border-subtle hover:bg-secondary"
              >
                Go to Daily Ops
              </Link>
            </div>
          </div>
        </div>
      );
    }
    return this.props.children;
  }
}

export default ErrorBoundary;
