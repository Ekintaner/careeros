import { PlanningEditor } from "@/features/planning/editor";
import "@/features/planning/planning.css";
export const metadata = { title: "Career tasks" };
export default async function Page({
  searchParams,
}: {
  searchParams: Promise<{ applicationId?: string }>;
}) {
  const { applicationId } = await searchParams;
  return <PlanningEditor tasks={true} applicationId={applicationId} />;
}
