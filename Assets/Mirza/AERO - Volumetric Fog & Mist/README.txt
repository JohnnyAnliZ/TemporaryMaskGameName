
AERO - Volumetric Fog & Mist:

A) Set your render pipeline asset to the one included in the package under Settings.
(make sure that asset and the renderer with the volumetric fog is active).

B) Alternatively, add a URP FullScreenPassRendererFeature and set the fog material.

You can use the included volumetric fog material, or copy/modify/create your own.
If you don't care for scattering/blur/mist, set *both* the remap min and max to 1.0.

Quality and performance are determined by steps and lighting features.

That's it!

Please see the live documentation for more information.

🔗 https://mirzabeig.notion.site/AERO-Volumetric-Fog-Mist-3c0d023ca81842509ad89c749307ac53

-Release Notes-

1.0.0 | February 12, 2026:

- Reboot, first release.