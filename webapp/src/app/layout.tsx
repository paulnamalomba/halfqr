import type { Metadata } from "next";
import { IBM_Plex_Mono, IBM_Plex_Sans, Space_Grotesk, Poppins } from "next/font/google";
import { SiteHeader } from "@/components/site-header";
import "./globals.css";

const headingFont = Poppins({
  subsets: ["latin"],
  variable: "--font-heading",
  weight: ["500", "600"],
});

const bodyFont = Poppins({
  subsets: ["latin"],
  variable: "--font-poppins",
  weight: ["400", "500", "600"],
});

const monoFont = Poppins({
  subsets: ["latin"],
  variable: "--font-mono",
  weight: ["400", "500"],
});

export const metadata: Metadata = {
  metadataBase: new URL("https://www.halfqr.com"),
  title: {
    default: "HalfQR | Styled QR Builder",
    template: "%s | HalfQR",
  },
  description: "Generate branded QR codes with async SVG and PNG rendering, dotted data modules, linear gradients, and centered logo uploads.",
  icons: {
    icon: [{ url: "/logos/favicon.ico", type: "image/x-icon" }],
    shortcut: ["/logos/favicon.ico"],
    apple: [{ url: "/logos/havqr_favicon_180x180.png", type: "image/png" }],
  },
  openGraph: {
    title: "HalfQR | Styled QR Builder",
    description: "Queue branded QR renders with live async worker output, finder styling, dotted data modules, and centered logo uploads.",
    url: "https://www.halfqr.com",
    siteName: "HalfQR",
    images: [{ url: "/logos/havqr_main_6000x3306.png", width: 1200, height: 661, alt: "HalfQR" }],
  },
  twitter: {
    card: "summary_large_image",
    title: "HalfQR | Styled QR Builder",
    description: "Design the QR in the browser. Render the final SVG and PNG on the worker.",
    images: ["/logos/havqr_main_6000x3306.png"],
  },
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body className={`site-body ${headingFont.variable} ${bodyFont.variable} ${monoFont.variable}`}>
        <div className="app-frame">
          <SiteHeader />
          {children}
        </div>
      </body>
    </html>
  );
}