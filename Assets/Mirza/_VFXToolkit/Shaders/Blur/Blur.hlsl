// Blurs.

#include "../Common.hlsl"

// Gaussian blur.

// Blur radius directly controls quality and blur amount.

// When using Amplify Shader Editor + _CameraOpaqueTexture,
// enable 'Use Sampling Macros' on the shader to force ASE to declare as texture2D, vs. sampler2D.

// Bias controls blur direction:

//  0.0 = blur in both directions of that axis.
//  1.0 = blur only in positive direction of that axis.
// -1.0 = blur only in negative direction of that axis.

#define MAX_GAUSSIAN_BLUR_QUALITY 32
#define GAUSSIAN_BLUR_KERNEL_SIZE (MAX_GAUSSIAN_BLUR_QUALITY * 2) + 1

void FillGaussianKernel(int quality, out float kernel[GAUSSIAN_BLUR_KERNEL_SIZE])
{
    // Pre-compute Gaussian kernel.

    float sigma = quality / 2.0;
    float sigmaSqr = sigma * sigma;

    float oneOverTauSigmaSqr = 1.0 / (TAU * sigmaSqr);
    
    for (int i = 0; i < (quality * 2) + 1; i++)
    {
        float x = float(i) - quality;
        kernel[i] = oneOverTauSigmaSqr * exp(-x * x / (2.0 * sigmaSqr));
    }
    
    // Fill remainder, else I'll get errors about the array not being fully initialized.
    
    for (i = (quality * 2) + 1; i < GAUSSIAN_BLUR_KERNEL_SIZE; i++)
    {
        kernel[i] = 0.0;
    }
}

void GaussianBlur_float(Texture2D tex, float2 uv, float2 texelSize, SamplerState samplerState, int quality, float2 radius, float2 directionBias, out float4 output)
{
    output = 0.0;
    
    // If no blur, return current pixel as-is.
    
    // Very useful when masking the radius. 
    // More masking (to zero radius) -> more FPS.
    
    // Noticeable (beneficial) difference in performance
    // when using this check on 4096 x 4096 with 8 quality.
    
    // Up to +20 FPS on RTX 4090 with a simple radial mask w/ pow(mask, 2.0).
    
    if (radius.x <= 0.0 && radius.y <= 0.0)
    {
        output = SAMPLE_TEXTURE2D(tex, samplerState, uv);
        return;
    }
    
	// Pre-calculations.
    
    float kernelWeightSum = 0.0;
    
    float2 directionBiasSign = sign(directionBias);
    float2 oneMinusDirectionBiasAbsMax = max(0.0, 1.0 - abs(directionBias));
    
    radius *= texelSize / quality;

    // Pre-compute Gaussian kernel.

    float kernel[GAUSSIAN_BLUR_KERNEL_SIZE];        
    FillGaussianKernel(quality, kernel);

    // Blur (average) the pixels around current pixel based on Gaussian kernel.
    
    [loop] // Force loop required for radius == 0.0 bool.
    for (int y = -quality; y <= quality; y++)
    {
        [loop]
        for (int x = -quality; x <= quality; x++)
        {
            // Apply bias to blur direction.                   
            
            // I could make the last part just float(x > 0) vs.            
            // (float(x) * sign(xBias)) > 0.0, but in that case I can't
            // automatically handle negative values for bias being used
            // to determine blur direction and would need to invert the
            // input radius what that axis. I figure the convenience
            // and clarity is worth the extra sign calculation.
            
            float2 bias = lerp(1.0, oneMinusDirectionBiasAbsMax, (float2(x, y) * directionBiasSign) > 0.0);
            
            float weightBias = bias.x * bias.y;
            
            // Skip if no blur in this direction.
            
            if (weightBias <= 0.0)
            {
                continue;
            }

            float2 offset = float2(x, y) * radius;

            float weight = kernel[quality + x] * kernel[quality + y] * weightBias;
            output += SAMPLE_TEXTURE2D(tex, samplerState, uv + offset) * weight;

            kernelWeightSum += weight;
        }
    }

	// Normalize accumulated blur colour by iterations and return.

    output /= kernelWeightSum;
}

// Pass in existing kernel, already filled with the correct values for the designated quality.

void GaussianBlurPreKernel_float(Texture2D tex, float2 uv, float2 texelSize, SamplerState samplerState, int quality, float2 radius, float2 directionBias, float kernel[GAUSSIAN_BLUR_KERNEL_SIZE], out float4 output)
{
    output = 0.0;
    
    if (radius.x <= 0.0 && radius.y <= 0.0)
    {
        output = SAMPLE_TEXTURE2D(tex, samplerState, uv);
        return;
    }
    
	// Pre-calculations.
    
    float kernelWeightSum = 0.0;
    
    float2 directionBiasSign = sign(directionBias);
    float2 oneMinusDirectionBiasAbsMax = max(0.0, 1.0 - abs(directionBias));
    
    radius *= texelSize / quality;
    
    // Blur (average) the pixels around current pixel based on Gaussian kernel.
    
    [loop] // Force loop required for radius == 0.0 bool.
    for (int y = -quality; y <= quality; y++)
    {
        [loop]
        for (int x = -quality; x <= quality; x++)
        {
            // Apply bias to blur direction.
            
            float2 bias = lerp(1.0, oneMinusDirectionBiasAbsMax, (float2(x, y) * directionBiasSign) > 0.0);
            
            float weightBias = bias.x * bias.y;
            
            // Skip if no blur in this direction.
            
            if (weightBias <= 0.0)
            {
                continue;
            }

            float2 offset = float2(x, y) * radius;

            float weight = kernel[quality + x] * kernel[quality + y] * weightBias;
            output += SAMPLE_TEXTURE2D(tex, samplerState, uv + offset) * weight;

            kernelWeightSum += weight;
        }
    }

	// Normalize accumulated blur colour by iterations and return.

    output /= kernelWeightSum;
}