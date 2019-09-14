﻿using UnityEngine;

namespace Fractals {

    public class FractalGenerator : MonoBehaviour {

        private const int GROUP_SIZE_X = 8;
        private const int GROUP_SIZE_Y = 8;

        private const string CALCULATE_ESCAPE_TIME_KERNEL_NAME = "CalculateEscapeTime";
        private const string COLOUR_ESCAPE_TIME_KERNEL_NAME = "ColourByEscapeTime";
        private const string CLEAR_KERNEL_NAME = "Clear";
        private const string RESULT_TEXTURE_NAME = "ResultTexture";
        private const string ESCAPE_TIME_BUFFER_NAME = "EscapeTimeBuffer";
        private const string MAX_ITERATIONS_NAME = "MaxIterations";
        private const string LOWER_LEFT_NAME = "LowerLeft";
        private const string SCALE_NAME = "Scale";

        [SerializeField]
        private ComputeShader mandelbrotComputeShader = default;

        [SerializeField]
        private int maxIterations = 10;

        private int calculateEscapeTimeKernelId;
        private int colourEscapeTimeKernelId;
        private int clearKernelId;

        private int resultTextureId;
        private int escapeTimeBufferId;
        private int maxIterationsId;
        private int lowerLeftId;
        private int scaleId;

        private int threadGroupsX;
        private int threadGroupsY;

        private RenderTexture resultTexture;
        private ComputeBuffer escapeTimeBuffer;

        private float scale;
        private Vector2 lowerLeft;

        private void Awake() {

            if (mandelbrotComputeShader == null) {
                Debug.Log("Shader not set");
                enabled = false;
            }

            if (GetComponent<Camera>() == null) {
                Debug.Log("Must be on a camera to work");
                enabled = false;
            }

            GetShaderIds();
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination) {
            Render(destination);
        }

        private void OnDestroy() {
            ReleaseStuff();
        }

        private void Render(RenderTexture destination) {

            if (ResolutionHasChanged()) {
                UpdateResolution();
                CalculateView();
                UpdateShaderParameters();
                mandelbrotComputeShader.Dispatch(clearKernelId, threadGroupsX, threadGroupsY, 1);
                mandelbrotComputeShader.Dispatch(calculateEscapeTimeKernelId, threadGroupsX, threadGroupsY, 1);
                mandelbrotComputeShader.Dispatch(colourEscapeTimeKernelId, threadGroupsX, threadGroupsY, 1);
            }

            Graphics.Blit(resultTexture, destination);
        }

        private bool ResolutionHasChanged() {
            return resultTexture == null || resultTexture.width != Screen.width || resultTexture.height != Screen.height;
        }

        private void GetShaderIds() {

            // TODO: Check that the returned ids are valid

            calculateEscapeTimeKernelId = mandelbrotComputeShader.FindKernel(CALCULATE_ESCAPE_TIME_KERNEL_NAME);
            colourEscapeTimeKernelId = mandelbrotComputeShader.FindKernel(COLOUR_ESCAPE_TIME_KERNEL_NAME);
            clearKernelId = mandelbrotComputeShader.FindKernel(CLEAR_KERNEL_NAME);

            resultTextureId = Shader.PropertyToID(RESULT_TEXTURE_NAME);
            escapeTimeBufferId = Shader.PropertyToID(ESCAPE_TIME_BUFFER_NAME);
            maxIterationsId = Shader.PropertyToID(MAX_ITERATIONS_NAME);
            lowerLeftId = Shader.PropertyToID(LOWER_LEFT_NAME);
            scaleId = Shader.PropertyToID(SCALE_NAME);

        }

        // TODO: Should be able to update later instead of hard-coding it
        private void CalculateView() {

            // The region of interest is (x in [-2.5, 0.5], y in [-1.5, 1.5])
            // Calculate the scale and lower left so that all of it is visible
            // regardless of the aspect ratio of the screen

            scale = 3f / Mathf.Min(Screen.width, Screen.height);
            lowerLeft = new Vector2(-2.5f, -1.5f);
        }

        private void UpdateShaderParameters() {
            mandelbrotComputeShader.SetFloat(scaleId, scale);
            mandelbrotComputeShader.SetVector(lowerLeftId, lowerLeft);
            mandelbrotComputeShader.SetInt(maxIterationsId, maxIterations);
        }

        private void UpdateResolution() {

            ReleaseStuff();

            resultTexture = new RenderTexture(Screen.width, Screen.height, 0, 
                                              RenderTextureFormat.ARGBFloat,
                                              RenderTextureReadWrite.Linear);
            resultTexture.enableRandomWrite = true;
            resultTexture.Create();

            threadGroupsX = Mathf.CeilToInt((float)Screen.width / GROUP_SIZE_X);
            threadGroupsY = Mathf.CeilToInt((float)Screen.height / GROUP_SIZE_Y);

            escapeTimeBuffer = new ComputeBuffer(Screen.width * Screen.height, sizeof(int));

            mandelbrotComputeShader.SetTexture(calculateEscapeTimeKernelId, resultTextureId, resultTexture);
            mandelbrotComputeShader.SetTexture(colourEscapeTimeKernelId, resultTextureId, resultTexture);
            mandelbrotComputeShader.SetTexture(clearKernelId, resultTextureId, resultTexture);
            mandelbrotComputeShader.SetBuffer(calculateEscapeTimeKernelId, escapeTimeBufferId, escapeTimeBuffer);
            mandelbrotComputeShader.SetBuffer(colourEscapeTimeKernelId, escapeTimeBufferId, escapeTimeBuffer);
            mandelbrotComputeShader.SetBuffer(clearKernelId, escapeTimeBufferId, escapeTimeBuffer);
        }

        private void ReleaseStuff() {
            resultTexture?.Release();
            escapeTimeBuffer?.Release();
        }

    }

}