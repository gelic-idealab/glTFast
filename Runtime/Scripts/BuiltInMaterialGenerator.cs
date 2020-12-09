﻿// Copyright 2020 Andreas Atteneder
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

#if ! GLTFAST_SHADER_GRAPH || UNITY_EDITOR

using System.Collections.Generic;
using UnityEngine;

namespace GLTFast {

    using Materials;
    using static Materials.StandardShaderHelper;
    using AlphaMode = Schema.Material.AlphaMode;

    public class BuiltInMaterialGenerator : MaterialGenerator {

        const string SHADER_PBR_METALLIC_ROUGHNESS = "glTF/PbrMetallicRoughness";
        const string SHADER_PBR_SPECULAR_GLOSSINESS = "glTF/PbrSpecularGlossiness";
        const string SHADER_UNLIT = "glTF/Unlit";

        Shader pbrMetallicRoughnessShader;
        Shader pbrSpecularGlossinessShader;
        Shader unlitShader;

        public override UnityEngine.Material GetDefaultMaterial() {
            return GetPbrMetallicRoughnessMaterial();
        }

        UnityEngine.Material GetPbrMetallicRoughnessMaterial(bool doubleSided=false) {
            if(pbrMetallicRoughnessShader==null) {
                pbrMetallicRoughnessShader = FindShader(SHADER_PBR_METALLIC_ROUGHNESS);
            }
            if(pbrMetallicRoughnessShader==null) {
                return null;
            }
            var mat = new Material(pbrMetallicRoughnessShader);
            if(doubleSided) {
                // Turn off back-face culling
                mat.SetFloat(cullModePropId,0);
#if UNITY_EDITOR
                mat.doubleSidedGI = true;
#endif
            }
            return mat;
        }

        UnityEngine.Material GetPbrSpecularGlossinessMaterial(bool doubleSided=false) {
            if(pbrSpecularGlossinessShader==null) {
                pbrSpecularGlossinessShader = FindShader(SHADER_PBR_SPECULAR_GLOSSINESS);
            }
            if(pbrSpecularGlossinessShader==null) {
                return null;
            }
            var mat = new Material(pbrSpecularGlossinessShader);
            if(doubleSided) {
                // Turn off back-face culling
                mat.SetFloat(cullModePropId,0);
#if UNITY_EDITOR
                mat.doubleSidedGI = true;
#endif
            }
            return mat;
        }

        UnityEngine.Material GetUnlitMaterial(bool doubleSided=false) {
            if(unlitShader==null) {
                unlitShader = FindShader(SHADER_UNLIT);
            }
            if(unlitShader==null) {
                return null;
            }
            var mat = new Material(unlitShader);
            if(doubleSided) {
                // Turn off back-face culling
                mat.SetFloat(cullModePropId,0);
#if UNITY_EDITOR
                mat.doubleSidedGI = true;
#endif
            }
            return mat;
        }

        public override UnityEngine.Material GenerateMaterial(
            Schema.Material gltfMaterial,
            ref Schema.Texture[] textures,
            ref Schema.Image[] schemaImages,
            ref Dictionary<int,Texture2D>[] imageVariants
        ) {
            UnityEngine.Material material;
            
            if (gltfMaterial.extensions!=null && gltfMaterial.extensions.KHR_materials_pbrSpecularGlossiness!=null) {
                material = GetPbrSpecularGlossinessMaterial(gltfMaterial.doubleSided);
            } else
            if (gltfMaterial.extensions.KHR_materials_unlit!=null) {
                material = GetUnlitMaterial(gltfMaterial.doubleSided);
            } else {
                material = GetPbrMetallicRoughnessMaterial(gltfMaterial.doubleSided);
            }

            if(material==null) return null;

            material.name = gltfMaterial.name;

            StandardShaderMode shaderMode = StandardShaderMode.Opaque;
            Color baseColorLinear = Color.white;

            if(gltfMaterial.alphaModeEnum == AlphaMode.MASK) {
                material.SetFloat(cutoffPropId, gltfMaterial.alphaCutoff);
                shaderMode = StandardShaderMode.Cutout;
            } else if(gltfMaterial.alphaModeEnum == AlphaMode.BLEND) {
                SetAlphaModeBlend( material );
                shaderMode = StandardShaderMode.Fade;
            }

            if (gltfMaterial.extensions != null) {
                // Specular glossiness
                Schema.PbrSpecularGlossiness specGloss = gltfMaterial.extensions.KHR_materials_pbrSpecularGlossiness;
                if (specGloss != null) {
                    baseColorLinear = specGloss.diffuseColor;
                    material.SetVector(specColorPropId, specGloss.specularColor);
                    material.SetFloat(glossinessPropId,specGloss.glossinessFactor);

                    TrySetTexture(specGloss.diffuseTexture,material,mainTexPropId,ref textures,ref schemaImages, ref imageVariants);

                    if (TrySetTexture(specGloss.specularGlossinessTexture,material,specGlossMapPropId,ref textures,ref schemaImages, ref imageVariants)) {
                        material.EnableKeyword(KW_SPEC_GLOSS_MAP);
                    }
                }
            }

            if (gltfMaterial.pbrMetallicRoughness!=null) {
                baseColorLinear = gltfMaterial.pbrMetallicRoughness.baseColor;
                material.SetFloat(metallicPropId, gltfMaterial.pbrMetallicRoughness.metallicFactor );
                material.SetFloat(roughnessPropId, gltfMaterial.pbrMetallicRoughness.roughnessFactor );

                TrySetTexture(
                    gltfMaterial.pbrMetallicRoughness.baseColorTexture,
                    material,
                    mainTexPropId,
                    ref textures,
                    ref schemaImages,
                    ref imageVariants
                    );
                
                if(TrySetTexture(gltfMaterial.pbrMetallicRoughness.metallicRoughnessTexture,material,metallicGlossMapPropId,ref textures,ref schemaImages, ref imageVariants)) {
                    material.EnableKeyword(KW_METALLIC_ROUGNESS_MAP);
                }
            }

            if(TrySetTexture(gltfMaterial.normalTexture,material,bumpMapPropId,ref textures,ref schemaImages, ref imageVariants)) {
                material.EnableKeyword(KW_NORMALMAP);
                material.SetFloat(bumpScalePropId,gltfMaterial.normalTexture.scale);
            }

            if(TrySetTexture(gltfMaterial.occlusionTexture,material,occlusionMapPropId,ref textures,ref schemaImages, ref imageVariants)) {
                material.EnableKeyword(KW_OCCLUSION);
            }

            if(TrySetTexture(gltfMaterial.emissiveTexture,material,emissionMapPropId,ref textures,ref schemaImages, ref imageVariants)) {
                material.EnableKeyword(KW_EMISSION);
            }

            if (gltfMaterial.extensions != null) {

                // Transmission - Approximation
                var transmission = gltfMaterial.extensions.KHR_materials_transmission;
                if (transmission != null) {
#if !GLTFAST_SHADER_GRAPH && UNITY_EDITOR
                    Debug.LogWarning("Chance of incorrect materials! glTF transmission is approximated when using built-in render pipeline!");
#endif
                    // Correct transmission is not supported in Built-In renderer
                    // This is an approximation for some corner cases
                    if (transmission.transmissionFactor > 0f && transmission.transmissionTexture.index < 0) {
                        var min = Mathf.Min(Mathf.Min(baseColorLinear.r, baseColorLinear.g), baseColorLinear.b);
                        var max = baseColorLinear.maxColorComponent;
                        if (max - min < .1f) {
                            // R/G/B components don't diverge too much
                            // -> white/grey/black-ish color
                            // -> Approximation via Transparent mode should be close to real transmission
                            shaderMode = StandardShaderMode.Transparent;
                            baseColorLinear.a *= 1-transmission.transmissionFactor;
                        } else {
                            // Color is somewhat saturated
                            // -> Fallback to Blend mode
                            // -> Dial down transmissionFactor by 50% to avoid material completely disappearing
                            // Shows at least some color tinting
                            shaderMode = StandardShaderMode.Fade;
                            baseColorLinear.a *= 1-transmission.transmissionFactor*0.5f;
                            // Premultiply color? Decided not to. I prefered vivid (but too bright) colors over desaturation effect. 
                            // baseColorLinear.r *= baseColorLinear.a;
                            // baseColorLinear.g *= baseColorLinear.a;
                            // baseColorLinear.b *= baseColorLinear.a;
                        }
                    }
                }
            }
            
            switch (shaderMode)
            {
                case StandardShaderMode.Cutout:
                    SetAlphaModeMask( material, gltfMaterial);
                    break;
                case StandardShaderMode.Fade:
                    SetAlphaModeBlend( material );
                    break;
                case StandardShaderMode.Transparent:
                    SetAlphaModeTransparent( material );
                    break;
                default:
                    SetOpaqueMode(material);
                    break;
            }

            material.color = baseColorLinear.gamma;
            
            if(gltfMaterial.emissive != Color.black) {
                material.SetColor(emissionColorPropId, gltfMaterial.emissive.gamma);
                material.EnableKeyword(KW_EMISSION);
            }

            return material;
        }
    }
}
#endif // !GLTFAST_SHADER_GRAPH
