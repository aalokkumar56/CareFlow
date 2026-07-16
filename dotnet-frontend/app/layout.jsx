import "@/index.css";
import "@/App.css";
import Providers from "./providers";

export const metadata = {
  title: "CureFlow",
  description: "Healthcare operations platform",
};

export default function RootLayout({ children }) {
  return (
    <html lang="en">
      <body>
        <Providers>{children}</Providers>
      </body>
    </html>
  );
}
