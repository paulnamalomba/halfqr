// Some react imports
import type { ComponentProps, ElementType } from "react";
import Image from "next/image";
import { classNameMod } from "@/utils/classname-modulariser";

type SiteTitleIconProps = Omit<ComponentProps<typeof Image>, 'src' | 'alt' | 'width' | 'height'>;

// We have our icon target at public/logos/halfqr_main_6000x3306.svg - stored as site asset publically rather than reloaded every time
export const SiteTitleIcon = ({ className, ...props }: SiteTitleIconProps) => {
    return (
        <Image
        src="/logos/halfqr_main_6000x3306.svg"
        alt="HalfQR"
        width={81}
        height={44}
        className={classNameMod('h-auto w-[148px] md:w-[200px] rounded-full', className)}
        {...props}
        />
    );
}

