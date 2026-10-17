import { PlanningEditor } from "@/features/planning/editor";
import "@/features/planning/planning.css";
export const metadata = { title: "Interviews" };
export default async function Page({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  return <PlanningEditor tasks={false} id={id} />;
}
