import { PlanningList } from "@/features/planning/list";
import "@/features/planning/planning.css";
export const metadata = { title: "Interviews" };
export default async function Page({
  searchParams,
}: {
  searchParams: Promise<{ applicationId?: string }>;
}) {
  const { applicationId } = await searchParams;
  return <PlanningList tasks={false} applicationId={applicationId} />;
}
