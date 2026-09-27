import { CompanyEditorPage } from "@/features/opportunities/company-editor";
export const metadata = { title: "Edit company" };
export default async function Page({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  return <CompanyEditorPage id={(await params).id} />;
}
