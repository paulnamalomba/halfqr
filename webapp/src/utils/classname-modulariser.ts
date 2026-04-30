// these imports are for the classNameMod function
// clsx is a utility for constructing className strings conditionally
// tailwind-merge is a utility for merging Tailwind CSS class names and resolving conflicts
import { clsx, type ClassValue } from "clsx";
import { twMerge } from "tailwind-merge";

// inputs is an array of class values that can be strings, arrays, or objects
export const classNameMod = (...inputs: ClassValue[]) => twMerge(clsx(inputs));

// woot