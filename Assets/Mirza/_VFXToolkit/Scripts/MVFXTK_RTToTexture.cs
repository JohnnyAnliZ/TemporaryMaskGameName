using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mirza.VFXToolKit
{
    [ExecuteAlways]
    public class MVFXTK_RTToTexture : MonoBehaviour
    {
        Texture2D texture;
        new ParticleSystem particleSystem;

        public RenderTexture renderTexture;
        public TextureFormat textureFormat = TextureFormat.ARGB32;

        ParticleSystem.ShapeModule particleSystem_shape;

        void Start()
        {

        }

        public static void RenderTextureToTexture2D(RenderTexture renderTexture, Texture2D texture)
        {
            // ReadPixels looks at the active RenderTexture.

            RenderTexture.active = renderTexture;

            texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            texture.Apply();
        }

        void Update()
        {
            if (!particleSystem)
            {
                particleSystem = GetComponent<ParticleSystem>();
            }

            if (texture == null || (texture.width != renderTexture.width || texture.height != renderTexture.height))
            {
                if (texture != null)
                {
                    DestroyImmediate(texture);
                }

                texture = new Texture2D(renderTexture.width, renderTexture.height, textureFormat, false)
                {
                    name = $"RTToTexture ({name})"
                };
            }

            //print(renderTexture.format);
            //print(texture.format);

            //print("");

            //Graphics.CopyTexture(renderTexture, texture);
            RenderTextureToTexture2D(renderTexture, texture);

            particleSystem_shape = particleSystem.shape;
            particleSystem_shape.texture = texture;
        }
    }
}