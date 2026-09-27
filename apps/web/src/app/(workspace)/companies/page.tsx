import { CompaniesPage } from "@/features/opportunities/lists";
export const metadata = { title: "Companies" };
export default async function Page({
  searchParams,
}: {
  searchParams: Promise<{ saved?: string }>;
}) {
  return <CompaniesPage saved={(await searchParams).saved === "1"} />;
}
