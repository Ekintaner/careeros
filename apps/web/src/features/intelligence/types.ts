export interface SkillInsight {
  skillId: string;
  name: string;
  jobCount: number;
  requiredJobs: number;
  preferredJobs: number;
  inProfile: boolean;
  confirmedResumes: number;
  status: "supported" | "evidenceGap" | "missing";
  action: string;
  weightedDemand: number;
  demandPercent: number;
  actionPriority: number;
  learningPriority: number;
}
export interface Intelligence {
  algorithm: string;
  state: string;
  summary: {
    totalJobs: number;
    analyzedJobs: number;
    unassessedJobs: number;
    distinctSkills: number;
    profileSkills: number;
    supportedSkills: number;
    evidenceGapSkills: number;
    missingSkills: number;
    earnedProfilePoints: number;
    possiblePoints: number;
    profileCoverage: number | null;
  };
  items: SkillInsight[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  historyAvailable: boolean;
}
export interface SkillJobs {
  skillId: string;
  name: string;
  items: {
    id: string;
    title: string;
    companyName: string;
    requirementLevel: string;
    archived: boolean;
    earnedProfilePoints: number;
    possiblePoints: number;
    profileCoverage: number | null;
  }[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
