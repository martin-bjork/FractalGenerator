using UnityEngine;

namespace Fractals {

    public class FractalGenerator : MonoBehaviour {

        private const int GROUP_SIZE_X = 8;
        private const int GROUP_SIZE_Y = 8;

        private const string CALCULATE_ESCAPE_TIME_KERNEL_NAME = "CalculateEscapeTime";
        private const string COLOUR_ESCAPE_TIME_KERNEL_NAME = "ColourByEscapeTime";
        private const string COLOUR_GRAYSCALE_KERNEL_NAME = "ColourByGrayscale";
        private const string CLEAR_KERNEL_NAME = "Clear";
        private const string RESULT_TEXTURE_NAME = "ResultTexture";
        private const string ESCAPE_TIME_BUFFER_NAME = "EscapeTimeBuffer";
        private const string MAX_ITERATIONS_NAME = "MaxIterations";
        private const string LOWER_LEFT_NAME = "LowerLeft";
        private const string SCALE_NAME = "Scale";
        private const string COLORS_NAME = "Colors";
        private const string COLOR_LEVELS_NAME = "ColorLevels";

        private const int LEFT_MOUSE_BUTTON_ID = 0;

        [SerializeField]
        private ComputeShader mandelbrotComputeShader = default;

        [SerializeField]
        private int maxIterations = 10;

        [SerializeField]
        private float scrollSensitivity = 0.1f;

        [SerializeField]
        private Color[] colors = default;

        [SerializeField]
        private int colorLevels = 100;

        private int calculateEscapeTimeKernelId;
        private int colourEscapeTimeKernelId;
        private int colourGrayscaleKernelId;
        private int clearKernelId;

        private int resultTextureId;
        private int escapeTimeBufferId;
        private int maxIterationsId;
        private int lowerLeftId;
        private int scaleId;
        private int colorsBufferId;
        private int colorLevelsId;

        private int threadGroupsX;
        private int threadGroupsY;

        private RenderTexture resultTexture;
        private ComputeBuffer escapeTimeBuffer;
        private ComputeBuffer colorsBuffer;

        private float scale;
        private Vector2 lowerLeft;

        private bool needsReRender = true;

        private Vector3 lastMousePosition;

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
            CalculateInitialView();
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination) {
            Render(destination);
        }

        private void Update() {
            UpdateView();
        }

        private void OnDestroy() {
            ReleaseStuff();
        }

        private void UpdateView() {

            Vector2 mousePan = GetMousePan();
            float scrollDelta = Input.mouseScrollDelta.y * scrollSensitivity;

            if (mousePan == Vector2.zero && scrollDelta == 0) {
                return;
            }
            lowerLeft -= mousePan * scale;

            Vector2 mousePosition = new Vector2(Input.mousePosition.x, Input.mousePosition.y);

            scale /= (1 + scrollDelta);
            lowerLeft += scrollDelta * scale * mousePosition;

            needsReRender = true;
        }

        private Vector2 GetMousePan() {
            if (Input.GetMouseButton(LEFT_MOUSE_BUTTON_ID)) {
                if (Input.GetMouseButtonDown(LEFT_MOUSE_BUTTON_ID)) {
                    lastMousePosition = Input.mousePosition;
                    return Vector2.zero;
                } else {
                    Vector3 mouseDelta = Input.mousePosition - lastMousePosition;
                    lastMousePosition = Input.mousePosition;
                    return new Vector2(mouseDelta.x, mouseDelta.y);
                }
            } else {
                return Vector2.zero;
            }
        }

        private void Render(RenderTexture destination) {

            if (ResolutionHasChanged()) {
                UpdateResolution();
                needsReRender = true;
            }

            if (needsReRender) {
                UpdateShaderParameters();
                mandelbrotComputeShader.Dispatch(clearKernelId, threadGroupsX, threadGroupsY, 1);
                mandelbrotComputeShader.Dispatch(calculateEscapeTimeKernelId, threadGroupsX, threadGroupsY, 1);
                //mandelbrotComputeShader.Dispatch(colourGrayscaleKernelId, threadGroupsX, threadGroupsY, 1);
                mandelbrotComputeShader.Dispatch(colourEscapeTimeKernelId, threadGroupsX, threadGroupsY, 1);
                needsReRender = false;
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
            colourGrayscaleKernelId = mandelbrotComputeShader.FindKernel(COLOUR_GRAYSCALE_KERNEL_NAME);
            clearKernelId = mandelbrotComputeShader.FindKernel(CLEAR_KERNEL_NAME);

            resultTextureId = Shader.PropertyToID(RESULT_TEXTURE_NAME);
            escapeTimeBufferId = Shader.PropertyToID(ESCAPE_TIME_BUFFER_NAME);
            maxIterationsId = Shader.PropertyToID(MAX_ITERATIONS_NAME);
            lowerLeftId = Shader.PropertyToID(LOWER_LEFT_NAME);
            scaleId = Shader.PropertyToID(SCALE_NAME);
            colorsBufferId = Shader.PropertyToID(COLORS_NAME);
            colorLevelsId = Shader.PropertyToID(COLOR_LEVELS_NAME);
        }

        private void CalculateInitialView() {

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
            mandelbrotComputeShader.SetInt(colorLevelsId, colorLevels);

            if (colors.Length != colorsBuffer?.count) {
                colorsBuffer?.Release();
                colorsBuffer = new ComputeBuffer(colors.Length, 4 * sizeof(float));
            }
            colorsBuffer.SetData(colors);
            mandelbrotComputeShader.SetBuffer(colourEscapeTimeKernelId, colorsBufferId, colorsBuffer);
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
            mandelbrotComputeShader.SetTexture(colourGrayscaleKernelId, resultTextureId, resultTexture);
            mandelbrotComputeShader.SetTexture(clearKernelId, resultTextureId, resultTexture);
            mandelbrotComputeShader.SetBuffer(calculateEscapeTimeKernelId, escapeTimeBufferId, escapeTimeBuffer);
            mandelbrotComputeShader.SetBuffer(colourEscapeTimeKernelId, escapeTimeBufferId, escapeTimeBuffer);
            mandelbrotComputeShader.SetBuffer(colourGrayscaleKernelId, escapeTimeBufferId, escapeTimeBuffer);
            mandelbrotComputeShader.SetBuffer(clearKernelId, escapeTimeBufferId, escapeTimeBuffer);
        }

        private void ReleaseStuff() {
            resultTexture?.Release();
            escapeTimeBuffer?.Release();
            colorsBuffer?.Release();
        }

    }

}
