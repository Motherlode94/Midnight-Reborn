//  Copyright(c) 2016, Michal Skalsky
//  All rights reserved.
//
//  Redistribution and use in source and binary forms, with or without modification,
//  are permitted provided that the following conditions are met:
//
//  1. Redistributions of source code must retain the above copyright notice,
//     this list of conditions and the following disclaimer.
//
//  2. Redistributions in binary form must reproduce the above copyright notice,
//     this list of conditions and the following disclaimer in the documentation
//     and/or other materials provided with the distribution.
//
//  3. Neither the name of the copyright holder nor the names of its contributors
//     may be used to endorse or promote products derived from this software without
//     specific prior written permission. 
//
//  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY
//  EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
//  OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.IN NO EVENT
//  SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
//  SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT
//  OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION)
//  HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR
//  TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE,
//  EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.



Shader "Hidden/EnviroBlurURP"
{
	Properties
	{
        _MainTex("Texture", any) = "" {} 
	}
		
	SubShader
	{
		// No culling or depth
		Cull Off ZWrite Off ZTest Always

		HLSLINCLUDE
        //--------------------------------------------------------------------------------------------
        // Downsample, bilateral blur and upsample config
        //--------------------------------------------------------------------------------------------        
        // method used to downsample depth buffer: 0 = min; 1 = max; 2 = min/max in chessboard pattern
        #define DOWNSAMPLE_DEPTH_MODE 2
        #define UPSAMPLE_DEPTH_THRESHOLD 1.5f
        #define BLUR_DEPTH_FACTOR 0.5 
        #define GAUSS_BLUR_DEVIATION 1.5        
        #define FULL_RES_BLUR_KERNEL_SIZE 7
        #define HALF_RES_BLUR_KERNEL_SIZE 5
        #define QUARTER_RES_BLUR_KERNEL_SIZE 6
        //--------------------------------------------------------------------------------------------
		 
		//#define PI 3.1415927f

#if ENVIROURP17
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
		
		TEXTURE2D_X_FLOAT(_CameraDepthTexture);
		SAMPLER(sampler_CameraDepthTexture);
		TEXTURE2D_X_FLOAT(_HalfResDepthBuffer);
		SAMPLER(sampler_HalfResDepthBuffer);
		TEXTURE2D_X_FLOAT(_QuarterResDepthBuffer);
		SAMPLER(sampler_QuarterResDepthBuffer);
		TEXTURE2D_X(_HalfResColor);
		SAMPLER(sampler_HalfResColor);
		TEXTURE2D_X(_QuarterResColor);
		SAMPLER(sampler_QuarterResColor);
		TEXTURE2D_X(_MainTex);
		SAMPLER(sampler_MainTex);


		float4 _MainTex_TexelSize;
        float4 _CameraDepthTexture_TexelSize;
        float4 _HalfResDepthBuffer_TexelSize;
        float4 _QuarterResDepthBuffer_TexelSize;

		struct appdata
		{
			uint vertex : SV_VertexID;
			float2 uv : TEXCOORD0;
			UNITY_VERTEX_INPUT_INSTANCE_ID
		};

		struct v2f
		{
			float2 uv : TEXCOORD0;
			float4 vertex : SV_POSITION;
			UNITY_VERTEX_OUTPUT_STEREO
		};

		struct v2fDownsample
		{
#if SHADER_TARGET > 40
			float2 uv : TEXCOORD0;
#else
			float2 uv00 : TEXCOORD0;
			float2 uv01 : TEXCOORD1;
			float2 uv10 : TEXCOORD2;
			float2 uv11 : TEXCOORD3;
#endif
			float4 vertex : SV_POSITION;
			UNITY_VERTEX_OUTPUT_STEREO
		};

		struct v2fUpsample
		{
			float2 uv : TEXCOORD0;
			float2 uv00 : TEXCOORD1;
			float2 uv01 : TEXCOORD2;
			float2 uv10 : TEXCOORD3;
			float2 uv11 : TEXCOORD4;
			float4 vertex : SV_POSITION;
			UNITY_VERTEX_OUTPUT_STEREO
		};


		v2f vert(appdata v)
		{
			v2f o;
			UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

			float4 pos = GetFullScreenTriangleVertexPosition(v.vertex);
            float2 uv  = GetFullScreenTriangleTexCoord(v.vertex);
            
            o.vertex = pos;
            o.uv  = DYNAMIC_SCALING_APPLY_SCALEBIAS(uv);

			return o;
		}


		//-----------------------------------------------------------------------------------------
		// vertDownsampleDepth
		//-----------------------------------------------------------------------------------------
		v2fDownsample vertDownsampleDepth(appdata v, float2 texelSize)
		{
			v2fDownsample o;
			UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

			float4 pos = GetFullScreenTriangleVertexPosition(v.vertex);
            float2 uv  = GetFullScreenTriangleTexCoord(v.vertex);
            
            o.vertex = pos;

#if SHADER_TARGET > 40
			o.uv  = DYNAMIC_SCALING_APPLY_SCALEBIAS(uv);
#else
			o.uv00 =  DYNAMIC_SCALING_APPLY_SCALEBIAS(uv); - 0.5 * texelSize.xy;
			o.uv10 = o.uv00 + float2(texelSize.x, 0);
			o.uv01 = o.uv00 + float2(0, texelSize.y);
			o.uv11 = o.uv00 + texelSize.xy;
#endif
			return o;
		}

		//-----------------------------------------------------------------------------------------
		// vertUpsample
		//-----------------------------------------------------------------------------------------
        v2fUpsample vertUpsample(appdata v, float2 texelSize)
        {
            v2fUpsample o;
			UNITY_SETUP_INSTANCE_ID(v);

            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
           
			float4 pos = GetFullScreenTriangleVertexPosition(v.vertex);
            float2 uv  = GetFullScreenTriangleTexCoord(v.vertex);
            
            o.vertex = pos;
			o.uv  = DYNAMIC_SCALING_APPLY_SCALEBIAS(uv);

            o.uv00 = DYNAMIC_SCALING_APPLY_SCALEBIAS(uv) - 0.5 * texelSize.xy;
            o.uv10 = o.uv00 + float2(texelSize.x, 0);
            o.uv01 = o.uv00 + float2(0, texelSize.y);
            o.uv11 = o.uv00 + texelSize.xy;
            return o; 
        }
#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)		

		//-----------------------------------------------------------------------------------------
		// BilateralUpsample
		//-----------------------------------------------------------------------------------------
		float4 BilateralUpsample(v2fUpsample input, Texture2DArray hiDepth, Texture2DArray loDepth, Texture2DArray loColor, SamplerState linearSampler, SamplerState pointSampler)
		{
            const float threshold = UPSAMPLE_DEPTH_THRESHOLD;
			float4 highResDepth = LinearEyeDepth(SAMPLE_TEXTURE2D_X(hiDepth, pointSampler, input.uv), _ZBufferParams).xxxx;

			float4 lowResDepth;

            lowResDepth[0] = LinearEyeDepth(SAMPLE_TEXTURE2D_X(loDepth, pointSampler, input.uv00), _ZBufferParams);
            lowResDepth[1] = LinearEyeDepth(SAMPLE_TEXTURE2D_X(loDepth, pointSampler, input.uv10), _ZBufferParams);
            lowResDepth[2] = LinearEyeDepth(SAMPLE_TEXTURE2D_X(loDepth, pointSampler, input.uv01), _ZBufferParams);
            lowResDepth[3] = LinearEyeDepth(SAMPLE_TEXTURE2D_X(loDepth, pointSampler, input.uv11), _ZBufferParams);

			float4 depthDiff = abs(lowResDepth - highResDepth);

			float accumDiff = dot(depthDiff, float4(1, 1, 1, 1)); 

			[branch]
			if (accumDiff < threshold) // small error, not an edge -> use bilinear filter
			{
				return SAMPLE_TEXTURE2D_X(loColor,linearSampler,input.uv);
			}

			// find nearest sample
			float minDepthDiff = depthDiff[0];
			float2 nearestUv = input.uv00;

			if (depthDiff[1] < minDepthDiff)
			{
				nearestUv = input.uv10;
				minDepthDiff = depthDiff[1];
			}

			if (depthDiff[2] < minDepthDiff)
			{
				nearestUv = input.uv01;
				minDepthDiff = depthDiff[2];
			}

			if (depthDiff[3] < minDepthDiff)
			{
				nearestUv = input.uv11;
				minDepthDiff = depthDiff[3];
			}
			return SAMPLE_TEXTURE2D_X(loColor,pointSampler,nearestUv);
		}

		//-----------------------------------------------------------------------------------------
		// DownsampleDepth
		//-----------------------------------------------------------------------------------------
		float DownsampleDepth(v2fDownsample input, Texture2DArray depthTexture, SamplerState depthSampler)
		{
#if SHADER_TARGET > 40
            float4 depth = depthTexture.Gather(depthSampler, input.uv);
#else
			float4 depth;
			depth.x = SAMPLE_TEXTURE2D_X(depthTexture,depthSampler,input.uv00).x;
			depth.y = SAMPLE_TEXTURE2D_X(depthTexture,depthSampler,input.uv01).x;
			depth.z = SAMPLE_TEXTURE2D_X(depthTexture,depthSampler,input.uv10).x;
			depth.w = SAMPLE_TEXTURE2D_X(depthTexture,depthSampler,input.uv11).x;

#endif

#if DOWNSAMPLE_DEPTH_MODE == 0 // min  depth
            return min(min(depth.x, depth.y), min(depth.z, depth.w));
#elif DOWNSAMPLE_DEPTH_MODE == 1 // max  depth
            return max(max(depth.x, depth.y), max(depth.z, depth.w));
#elif DOWNSAMPLE_DEPTH_MODE == 2 // min/max depth in chessboard pattern

			float minDepth = min(min(depth.x, depth.y), min(depth.z, depth.w));
			float maxDepth = max(max(depth.x, depth.y), max(depth.z, depth.w));

			// chessboard pattern
			int2 position = input.vertex.xy % 2;
			int index = position.x + position.y;
			return index == 1 ? minDepth : maxDepth;
#endif
		}
		
		//-----------------------------------------------------------------------------------------
		// GaussianWeight
		//-----------------------------------------------------------------------------------------
		float GaussianWeight(float offset, float deviation)
		{
			float weight = 1.0f / sqrt(2.0f * PI * deviation * deviation);
			weight *= exp(-(offset * offset) / (2.0f * deviation * deviation));
			return weight;
		}

		//-----------------------------------------------------------------------------------------
		// BilateralBlur
		//-----------------------------------------------------------------------------------------
		float4 BilateralBlur(v2f input, int2 direction, Texture2DArray depth, SamplerState depthSampler, const int kernelRadius, float2 pixelSize)
		{ 
			//const float deviation = kernelRadius / 2.5;
			const float deviation = kernelRadius / GAUSS_BLUR_DEVIATION; // make it really strong

			float2 uv = input.uv;
			float4 centerColor = SAMPLE_TEXTURE2D_X(_MainTex,sampler_MainTex,uv );

			float4 color = centerColor;
			//return float4(color, 1);

			float centerDepth = LinearEyeDepth(SAMPLE_TEXTURE2D_X(depth,depthSampler,uv), _ZBufferParams);

			float weightSum = 0;

			// gaussian weight is computed from constants only -> will be computed in compile time
            float weight = GaussianWeight(0, deviation);
			color *= weight;
			weightSum += weight;
						
			[unroll] for (int i = -kernelRadius; i < 0; i += 1)
			{
                float2 offset = (direction * i);
 
                float4 sampleColor = SAMPLE_TEXTURE2D_X(_MainTex,sampler_MainTex,input.uv + offset * _MainTex_TexelSize.xy);   
			    float sampleDepth = LinearEyeDepth(SAMPLE_TEXTURE2D_X(depth,depthSampler, input.uv + offset * _MainTex_TexelSize.xy).x, _ZBufferParams);

				float depthDiff = abs(centerDepth - sampleDepth);
                float dFactor = depthDiff * BLUR_DEPTH_FACTOR;
				float w = exp(-(dFactor * dFactor));

				// gaussian weight is computed from constants only -> will be computed in compile time
				weight = GaussianWeight(i, deviation) * w;

				color += weight * sampleColor;
				weightSum += weight;
			}

			[unroll] for (int k = 1; k <= kernelRadius; k += 1) 
			{
				float2 offset = (direction * k);
          
				float4 sampleColor = SAMPLE_TEXTURE2D_X(_MainTex,sampler_MainTex,input.uv + offset * _MainTex_TexelSize.xy ); 
				float sampleDepth = LinearEyeDepth(SAMPLE_TEXTURE2D_X(depth,depthSampler,input.uv + offset * _MainTex_TexelSize.xy ).x , _ZBufferParams);

				float depthDiff = abs(centerDepth - sampleDepth);
                float dFactor = depthDiff * BLUR_DEPTH_FACTOR;
				float w = exp(-(dFactor * dFactor));
				
				// gaussian weight is computed from constants only -> will be computed in compile time
				weight = GaussianWeight(k, deviation) * w;
 
				color += weight * sampleColor;
				weightSum += weight;
			}

			color /= weightSum;
			return float4(color);
		}


#else
		//-----------------------------------------------------------------------------------------
		// BilateralUpsample
		//-----------------------------------------------------------------------------------------
		float4 BilateralUpsample(v2fUpsample input, Texture2D hiDepth, Texture2D loDepth, Texture2D loColor, SamplerState linearSampler, SamplerState pointSampler)
		{
            const float threshold = UPSAMPLE_DEPTH_THRESHOLD;
			float4 highResDepth = LinearEyeDepth(SAMPLE_TEXTURE2D_X(hiDepth, pointSampler, input.uv), _ZBufferParams).xxxx;

			float4 lowResDepth;

            lowResDepth[0] = LinearEyeDepth(SAMPLE_TEXTURE2D_X(loDepth, pointSampler, input.uv00), _ZBufferParams);
            lowResDepth[1] = LinearEyeDepth(SAMPLE_TEXTURE2D_X(loDepth, pointSampler, input.uv10), _ZBufferParams);
            lowResDepth[2] = LinearEyeDepth(SAMPLE_TEXTURE2D_X(loDepth, pointSampler, input.uv01), _ZBufferParams);
            lowResDepth[3] = LinearEyeDepth(SAMPLE_TEXTURE2D_X(loDepth, pointSampler, input.uv11), _ZBufferParams);

			float4 depthDiff = abs(lowResDepth - highResDepth);

			float accumDiff = dot(depthDiff, float4(1, 1, 1, 1)); 

			[branch]
			if (accumDiff < threshold) // small error, not an edge -> use bilinear filter
			{
				return SAMPLE_TEXTURE2D_X(loColor,linearSampler,input.uv);
			}

			// find nearest sample
			float minDepthDiff = depthDiff[0];
			float2 nearestUv = input.uv00;

			if (depthDiff[1] < minDepthDiff)
			{
				nearestUv = input.uv10;
				minDepthDiff = depthDiff[1];
			}

			if (depthDiff[2] < minDepthDiff)
			{
				nearestUv = input.uv01;
				minDepthDiff = depthDiff[2];
			}

			if (depthDiff[3] < minDepthDiff)
			{
				nearestUv = input.uv11;
				minDepthDiff = depthDiff[3];
			}
			return SAMPLE_TEXTURE2D_X(loColor,pointSampler,nearestUv);
		}

		//-----------------------------------------------------------------------------------------
		// DownsampleDepth
		//-----------------------------------------------------------------------------------------
		float DownsampleDepth(v2fDownsample input, Texture2D depthTexture, SamplerState depthSampler)
		{
#if SHADER_TARGET > 40
            float4 depth = depthTexture.Gather(depthSampler, input.uv);
#else
			float4 depth;
			depth.x = SAMPLE_TEXTURE2D_X(depthTexture,depthSampler,input.uv00).x;
			depth.y = SAMPLE_TEXTURE2D_X(depthTexture,depthSampler,input.uv01).x;
			depth.z = SAMPLE_TEXTURE2D_X(depthTexture,depthSampler,input.uv10).x;
			depth.w = SAMPLE_TEXTURE2D_X(depthTexture,depthSampler,input.uv11).x;

#endif

#if DOWNSAMPLE_DEPTH_MODE == 0 // min  depth
            return min(min(depth.x, depth.y), min(depth.z, depth.w));
#elif DOWNSAMPLE_DEPTH_MODE == 1 // max  depth
            return max(max(depth.x, depth.y), max(depth.z, depth.w));
#elif DOWNSAMPLE_DEPTH_MODE == 2 // min/max depth in chessboard pattern

			float minDepth = min(min(depth.x, depth.y), min(depth.z, depth.w));
			float maxDepth = max(max(depth.x, depth.y), max(depth.z, depth.w));

			// chessboard pattern
			int2 position = input.vertex.xy % 2;
			int index = position.x + position.y;
			return index == 1 ? minDepth : maxDepth;
#endif
		}
		
		//-----------------------------------------------------------------------------------------
		// GaussianWeight
		//-----------------------------------------------------------------------------------------
		float GaussianWeight(float offset, float deviation)
		{
			float weight = 1.0f / sqrt(2.0f * PI * deviation * deviation);
			weight *= exp(-(offset * offset) / (2.0f * deviation * deviation));
			return weight;
		}

		//-----------------------------------------------------------------------------------------
		// BilateralBlur
		//-----------------------------------------------------------------------------------------
		float4 BilateralBlur(v2f input, int2 direction, Texture2D depth, SamplerState depthSampler, const int kernelRadius, float2 pixelSize)
		{ 
			//const float deviation = kernelRadius / 2.5;
			const float deviation = kernelRadius / GAUSS_BLUR_DEVIATION; // make it really strong

			float2 uv = input.uv;
			float4 centerColor = SAMPLE_TEXTURE2D_X(_MainTex,sampler_MainTex,uv );

			float4 color = centerColor;
			//return float4(color, 1);

			float centerDepth = LinearEyeDepth(SAMPLE_TEXTURE2D_X(depth,depthSampler,uv), _ZBufferParams);

			float weightSum = 0;

			// gaussian weight is computed from constants only -> will be computed in compile time
            float weight = GaussianWeight(0, deviation);
			color *= weight;
			weightSum += weight;
						
			[unroll] for (int i = -kernelRadius; i < 0; i += 1)
			{
                float2 offset = (direction * i);
 
                float4 sampleColor = SAMPLE_TEXTURE2D_X(_MainTex,sampler_MainTex,input.uv + offset * _MainTex_TexelSize.xy);   
			    float sampleDepth = LinearEyeDepth(SAMPLE_TEXTURE2D_X(depth,depthSampler, input.uv + offset * _MainTex_TexelSize.xy).x, _ZBufferParams);

				float depthDiff = abs(centerDepth - sampleDepth);
                float dFactor = depthDiff * BLUR_DEPTH_FACTOR;
				float w = exp(-(dFactor * dFactor));

				// gaussian weight is computed from constants only -> will be computed in compile time
				weight = GaussianWeight(i, deviation) * w;

				color += weight * sampleColor;
				weightSum += weight;
			}

			[unroll] for (int k = 1; k <= kernelRadius; k += 1) 
			{
				float2 offset = (direction * k);
          
				float4 sampleColor = SAMPLE_TEXTURE2D_X(_MainTex,sampler_MainTex,input.uv + offset * _MainTex_TexelSize.xy ); 
				float sampleDepth = LinearEyeDepth(SAMPLE_TEXTURE2D_X(depth,depthSampler,input.uv + offset * _MainTex_TexelSize.xy ).x , _ZBufferParams);

				float depthDiff = abs(centerDepth - sampleDepth);
                float dFactor = depthDiff * BLUR_DEPTH_FACTOR;
				float w = exp(-(dFactor * dFactor));
				
				// gaussian weight is computed from constants only -> will be computed in compile time
				weight = GaussianWeight(k, deviation) * w;
 
				color += weight * sampleColor;
				weightSum += weight;
			}

			color /= weightSum;
			return float4(color);
		}
#endif

#else
		struct appdata
		{
			float4 vertex : POSITION;
			float2 uv : TEXCOORD0;
		};

		struct v2f
		{
			float2 uv : TEXCOORD0;
			float4 vertex : SV_POSITION;
		};

		struct v2fDownsample
		{
			float2 uv : TEXCOORD0;
			float4 vertex : SV_POSITION;
		};

		struct v2fUpsample
		{
			float2 uv : TEXCOORD0;
			float4 vertex : SV_POSITION;
		};


		v2f vert (appdata v)
		{
			v2f o;
			o.vertex = v.vertex;
			o.uv = v.uv;
			return o;
		}

		v2fUpsample vertUpsample(appdata v, float2 texelSize)
		{
			v2fUpsample o;
			o.vertex = v.vertex;
			o.uv = v.uv;
			return o;
		}

		v2fDownsample vertDownsampleDepth(appdata v, float2 texelSize)
		{
			v2fDownsample o;
			o.vertex = v.vertex;
			o.uv = v.uv;
			return o;
		}

#endif

		ENDHLSL

		// pass 0 - horizontal blur (hires)
		Pass
		{

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment horizontalFrag
            #pragma target 3.5
			#pragma exclude_renderers gles
			#pragma multi_compile __ ENVIROURP17	
			
			float4 horizontalFrag(v2f input) : SV_Target
			{
				#if ENVIROURP17
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return BilateralBlur(input, int2(1, 0), _CameraDepthTexture, sampler_CameraDepthTexture, FULL_RES_BLUR_KERNEL_SIZE, _CameraDepthTexture_TexelSize.xy);
				#else
				return float4(0,0,0,0);
				#endif
			}

			ENDHLSL
		}

		// pass 1 - vertical blur (hires)
		Pass
		{

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment verticalFrag
			#pragma target 3.5
			#pragma exclude_renderers gles
			#pragma multi_compile __ ENVIROURP17	

			float4 verticalFrag(v2f input) : SV_Target
			{ 
				#if ENVIROURP17
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return BilateralBlur(input, int2(0, 1), _CameraDepthTexture, sampler_CameraDepthTexture, FULL_RES_BLUR_KERNEL_SIZE, _CameraDepthTexture_TexelSize);
				#else
				return float4(0,0,0,0);
				#endif
			}

			ENDHLSL
		}

		// pass 2 - horizontal blur (lores)
		Pass
		{

			HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment horizontalFrag
            #pragma target 3.5
			#pragma exclude_renderers gles
			#pragma multi_compile __ ENVIROURP17	

			float4 horizontalFrag(v2f input) : SV_Target
			{
				#if ENVIROURP17
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
				return BilateralBlur(input, int2(1, 0), _HalfResDepthBuffer, sampler_HalfResDepthBuffer, HALF_RES_BLUR_KERNEL_SIZE, _HalfResDepthBuffer_TexelSize);
				#else
					return float4(0,0,0,0);
					#endif
			}

		ENDHLSL
		}

		// pass 3 - vertical blur (lores)
		Pass
		{
 
			HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment verticalFrag
            #pragma target 3.5
			#pragma exclude_renderers gles
			#pragma multi_compile __ ENVIROURP17	

			float4 verticalFrag(v2f input) : SV_Target
			{
				#if ENVIROURP17
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
           		return BilateralBlur(input, int2(0, 1), _HalfResDepthBuffer, sampler_HalfResDepthBuffer, HALF_RES_BLUR_KERNEL_SIZE, _HalfResDepthBuffer_TexelSize);
				#else
				return float4(0,0,0,0);
				#endif	
			}

		ENDHLSL
		}

		// pass 4 - downsample depth to half
		Pass
		{
	 
			HLSLPROGRAM
			#pragma vertex vertHalfDepth
			#pragma fragment frag
           // #pragma target gl4.1
         	#pragma target 3.5
			#pragma exclude_renderers gles
			#pragma multi_compile __ ENVIROURP17	

			v2fDownsample vertHalfDepth(appdata v)
			{
				#if ENVIROURP17
                return vertDownsampleDepth(v, _CameraDepthTexture_TexelSize);
				#else
				v2fDownsample o;
				o.vertex = v.vertex;
				o.uv = v.uv;
				return o;
				#endif
			}

			float4 frag(v2fDownsample input) : SV_Target
			{
				#if ENVIROURP17
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
				float depth = DownsampleDepth(input, _CameraDepthTexture, sampler_CameraDepthTexture);
                return float4(depth,depth,depth,depth);
				#else
				return float4(0,0,0,0);
				#endif
			}

			ENDHLSL
		}

		// pass 5 - bilateral upsample
		Pass
		{
	
			Blend One Zero

			HLSLPROGRAM
			#pragma vertex vertUpsampleToFull
			#pragma fragment frag		
            #pragma target 3.5
			#pragma exclude_renderers gles
			#pragma multi_compile __ ENVIROURP17	

			v2fUpsample vertUpsampleToFull(appdata v)
			{
				#if ENVIROURP17
                return vertUpsample(v, _HalfResDepthBuffer_TexelSize);
				#else
				v2fUpsample o;
				o.vertex = v.vertex;
				o.uv = v.uv;
				return o;
				#endif
			}
			float4 frag(v2fUpsample input) : SV_Target
			{
				#if ENVIROURP17
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
				return BilateralUpsample(input, _CameraDepthTexture, _HalfResDepthBuffer, _HalfResColor, sampler_HalfResColor, sampler_HalfResDepthBuffer);
				#else
				return float4(0,0,0,0);
				#endif
			}

			ENDHLSL
		}

		// pass 6 - downsample depth to quarter
		Pass
		{

			HLSLPROGRAM
            #pragma vertex vertQuarterDepth
            #pragma fragment frag
            //#pragma target gl4.1
            #pragma target 3.5
			#pragma exclude_renderers gles
			#pragma multi_compile __ ENVIROURP17

			v2fDownsample vertQuarterDepth(appdata v)
			{
				#if ENVIROURP17
                return vertDownsampleDepth(v, _HalfResDepthBuffer_TexelSize);
				#else
				v2fDownsample o;
				o.vertex = v.vertex;
				o.uv = v.uv;
				return o;
				#endif
			}

			float4 frag(v2fDownsample input) : SV_Target
			{
				#if ENVIROURP17
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
				float depth = DownsampleDepth(input, _HalfResDepthBuffer, sampler_HalfResDepthBuffer);
                return float4(depth,depth,depth,depth);
				#else
				return float4(0,0,0,0);
				#endif
			}
			ENDHLSL
		}

		// pass 7 - bilateral upsample quarter to full
		Pass
		{
			Blend One Zero

			HLSLPROGRAM
            #pragma vertex vertUpsampleToFull
            #pragma fragment frag		
            #pragma target 3.5
			#pragma exclude_renderers gles
			#pragma multi_compile __ ENVIROURP17	

			v2fUpsample vertUpsampleToFull(appdata v)
			{
				#if ENVIROURP17
                return vertUpsample(v, _QuarterResDepthBuffer_TexelSize);
				#else
				v2fUpsample o;
				o.vertex = v.vertex;
				o.uv = v.uv;
				return o;
				#endif
			}
			float4 frag(v2fUpsample input) : SV_Target
			{
				#if ENVIROURP17
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return BilateralUpsample(input, _CameraDepthTexture, _QuarterResDepthBuffer, _QuarterResColor, sampler_QuarterResColor, sampler_QuarterResDepthBuffer);
				#else
				return float4(0,0,0,0);
				#endif
			}

			ENDHLSL
		}

		// pass 8 - horizontal blur (quarter res)
		Pass
		{
			HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment horizontalFrag
            #pragma target 3.5
			#pragma exclude_renderers gles
			#pragma multi_compile __ ENVIROURP17	

			float4 horizontalFrag(v2f input) : SV_Target
			{
				#if ENVIROURP17
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return BilateralBlur(input, int2(1, 0), _QuarterResDepthBuffer, sampler_QuarterResDepthBuffer, QUARTER_RES_BLUR_KERNEL_SIZE, _QuarterResDepthBuffer_TexelSize.xy);
				#else
				return float4(0,0,0,0);
				#endif
			}

			ENDHLSL
		}

		// pass 9 - vertical blur (quarter res)
		Pass
		{
			HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment verticalFrag
            #pragma target 3.5
			#pragma exclude_renderers gles
			#pragma multi_compile __ ENVIROURP17	

			float4 verticalFrag(v2f input) : SV_Target
			{
				#if ENVIROURP17
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return BilateralBlur(input, int2(0, 1), _QuarterResDepthBuffer, sampler_QuarterResDepthBuffer, QUARTER_RES_BLUR_KERNEL_SIZE, _QuarterResDepthBuffer_TexelSize.xy);
				#else
				return float4(0,0,0,0);
				#endif
			}

			ENDHLSL
		}

		// pass 10 - downsample depth to half (fallback for DX10)
		Pass
		{
			
			HLSLPROGRAM
			#pragma vertex vertHalfDepth
			#pragma fragment frag
			#pragma target 3.5
			#pragma exclude_renderers gles
			#pragma multi_compile __ ENVIROURP17	

			v2fDownsample vertHalfDepth(appdata v)
			{
				#if ENVIROURP17
				return vertDownsampleDepth(v, _CameraDepthTexture_TexelSize);
				#else
				v2fDownsample o;
				o.vertex = v.vertex;
				o.uv = v.uv;
				return o;
				#endif
			}

			float4 frag(v2fDownsample input) : SV_Target
			{
				#if ENVIROURP17
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
				float depth = DownsampleDepth(input, _CameraDepthTexture, sampler_CameraDepthTexture);
				return float4(depth,depth,depth,depth);
				#else
				return float4(0,0,0,0);
				#endif
			} 

			ENDHLSL
		}

		// pass 11 - downsample depth to quarter (fallback for DX10)
		Pass
		{
			HLSLPROGRAM
			#pragma vertex vertQuarterDepth
			#pragma fragment frag
			#pragma target 3.5
			#pragma exclude_renderers gles
			#pragma multi_compile __ ENVIROURP17	

			v2fDownsample vertQuarterDepth(appdata v)
			{
				#if ENVIROURP17
				return vertDownsampleDepth(v, _HalfResDepthBuffer_TexelSize);
				#else
				v2fDownsample o;
				o.vertex = v.vertex;
				o.uv = v.uv;
				return o;
				#endif
			}
			 
			float4 frag(v2fDownsample input) : SV_Target
			{
				#if ENVIROURP17
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
				float depth = DownsampleDepth(input, _HalfResDepthBuffer, sampler_HalfResDepthBuffer);
				return float4(depth,depth,depth,depth);
				#else
				return float4(0,0,0,0);
				#endif
			}

			ENDHLSL
		}
	}
}
