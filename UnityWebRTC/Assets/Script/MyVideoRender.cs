using Microsoft.MixedReality.WebRTC;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

public class MyVideoRender : MonoBehaviour
{
    /// <summary>
    /// Msc FPS
    /// </summary>
    [Tooltip("Max playback framerate, in frames per second")]
    [Range(0.001f, 120f)]
    public float MaxFramerate = 30f;

    public bool EnableStatistics = true;

    /// <summary>
    /// A textmesh onto which frame load stat data will be written
    /// </summary>
    /// <remarks>
    /// This is how fast the frames are given from the underlying implementation
    /// </remarks>
    [Tooltip("A textmesh onto which frame load stat data will be written")]
    public TextMesh FrameLoadStatHolder;

    /// <summary>
    /// A textmesh onto which frame present stat data will be written
    /// </summary>
    /// <remarks>
    /// This is how fast we render frames to the display
    /// </remarks>
    [Tooltip("A textmesh onto which frame present stat data will be written")]
    public TextMesh FramePresentStatHolder;

    /// <summary>
    /// A textmesh into which frame skip stat dta will be written
    /// </summary>
    /// <remarks>
    /// This is how often we skip presenting an underlying frame
    /// </remarks>
    [Tooltip("A textmesh onto which frame skip stat data will be written")]
    public TextMesh FrameSkipStatHolder;

    public RenderTexture renderTexture;

    /// <summary>
    /// The analysis result text
    /// </summary>
    private TextMesh labelText;

    /// <summary>
    /// Internal reference to the attached texture
    /// </summary>
    private Texture2D _textureY = null; // also used for ARGB32
    private Texture2D _textureU = null;
    private Texture2D _textureV = null;

    private float _minUpdateDelay;

    private float lastUpdateTime = 0.0f;

    private Material videoMaterial;

    private VideoFrameQueue<I420AVideoFrameStorage> _i420aFrameQueue = null;

    private ProfilerMarker displayStatsMarker = new ProfilerMarker("DisplayStats");
    private ProfilerMarker loadTextureDataMarker = new ProfilerMarker("LoadTextureData");
    private ProfilerMarker uploadTextureToGpuMarker = new ProfilerMarker("UploadTextureToGPU");

    private bool _getimage = true;

    private string imagestr = "";

    // Start is called before the first frame update
    void Start()
    {
        CreateEmptyVideoTextures();
    }

    public void StartRender(IVideoSource source) {
        bool isRemote = (source is RemoteVideoTrack);
        int frameQueueSize = (isRemote ? 5 : 3);

        switch (source.FrameEncoding)
        {
            case VideoEncoding.I420A:
            _i420aFrameQueue = new VideoFrameQueue<I420AVideoFrameStorage>(frameQueueSize);
            source.I420AVideoFrameReady += I420AVideoFrameReady;
            break;
        }
    }

    public void StopRendering(IVideoSource _) {
        // Clear the video display to not confuse the user who could otherwise
        // think that the video is still playing but is lagging/frozen.
        CreateEmptyVideoTextures();
    }

    // Update is called once per frame
    void Update()
    {
        if (_i420aFrameQueue != null)
        {
            var curTime = Time.time;
            if (curTime - lastUpdateTime >= _minUpdateDelay)
            {
                if (_i420aFrameQueue != null)
                {
                    TryProcessI420AFrame();
                }
                lastUpdateTime = curTime;
            }

            if (EnableStatistics)
            {
                // Share our stats values, if possible.
                using (var profileScope = displayStatsMarker.Auto())
                {
                    IVideoFrameQueue stats = (_i420aFrameQueue != null ? (IVideoFrameQueue)_i420aFrameQueue : null);
                    if (FrameLoadStatHolder != null)
                    {
                        FrameLoadStatHolder.text = stats.QueuedFramesPerSecond.ToString("F2");
                    }
                    if (FramePresentStatHolder != null)
                    {
                        FramePresentStatHolder.text = stats.DequeuedFramesPerSecond.ToString("F2");
                    }
                    if (FrameSkipStatHolder != null)
                    {
                        FrameSkipStatHolder.text = stats.DroppedFramesPerSecond.ToString("F2");
                    }
                }
            }
        }
    }

    private void CreateEmptyVideoTextures() {
        // Create a default checkboard texture which visually indicates
        // that no data is available. This is useful for debugging and
        // for the user to know about the state of the video.
        _textureY = new Texture2D(2, 2);
        _textureY.SetPixel(0, 0, Color.blue);
        _textureY.SetPixel(1, 1, Color.blue);
        _textureY.Apply();
        _textureU = new Texture2D(2, 2);
        _textureU.SetPixel(0, 0, Color.blue);
        _textureU.SetPixel(1, 1, Color.blue);
        _textureU.Apply();
        _textureV = new Texture2D(2, 2);
        _textureV.SetPixel(0, 0, Color.blue);
        _textureV.SetPixel(1, 1, Color.blue);
        _textureV.Apply();

        // Assign that texture to the video player's Renderer component
        videoMaterial = GetComponent<Renderer>().material;
        if (_i420aFrameQueue != null)
        {
            videoMaterial.SetTexture("_YPlane", _textureY);
            videoMaterial.SetTexture("_UPlane", _textureU);
            videoMaterial.SetTexture("_VPlane", _textureV);
        }
    }

    protected void I420AVideoFrameReady(I420AVideoFrame frame) {
        // This callback is generally from a non-UI thread, but Unity object access is only allowed
        // on the main UI thread, so defer to that point.
        _i420aFrameQueue.Enqueue(frame);
    }

    private void TryProcessI420AFrame() {
        if (_i420aFrameQueue.TryDequeue(out I420AVideoFrameStorage frame))
        {
            int lumaWidth = (int)frame.Width;
            int lumaHeight = (int)frame.Height;
            if (_textureY == null || (_textureY.width != lumaWidth || _textureY.height != lumaHeight))
            {
                _textureY = new Texture2D(lumaWidth, lumaHeight, TextureFormat.R8, mipChain: false);
                videoMaterial.SetTexture("_YPlane", _textureY);
            }
            int chromaWidth = lumaWidth / 2;
            int chromaHeight = lumaHeight / 2;
            if (_textureU == null || (_textureU.width != chromaWidth || _textureU.height != chromaHeight))
            {
                _textureU = new Texture2D(chromaWidth, chromaHeight, TextureFormat.R8, mipChain: false);
                videoMaterial.SetTexture("_UPlane", _textureU);
            }
            if (_textureV == null || (_textureV.width != chromaWidth || _textureV.height != chromaHeight))
            {
                _textureV = new Texture2D(chromaWidth, chromaHeight, TextureFormat.R8, mipChain: false);
                videoMaterial.SetTexture("_VPlane", _textureV);
            }
            using (var profileScope = loadTextureDataMarker.Auto())
            {
                unsafe
                {
                    fixed (void* buffer = frame.Buffer)
                    {
                        var src = new IntPtr(buffer);
                        int lumaSize = lumaWidth * lumaHeight;
                        _textureY.LoadRawTextureData(src, lumaSize);
                        src += lumaSize;
                        int chromaSize = chromaWidth * chromaHeight;
                        _textureU.LoadRawTextureData(src, chromaSize);
                        src += chromaSize;
                        _textureV.LoadRawTextureData(src, chromaSize);
                    }
                }
            }

            // Upload from system memory to GPU
            using (var profileScope = uploadTextureToGpuMarker.Auto())
            {
                _textureY.Apply();
                _textureU.Apply();
                _textureV.Apply();

                // Update image
                if (_getimage)
                    GetImage();
            }
            
            // Get render result
            Graphics.Blit(renderTexture, videoMaterial);

            // Recycle the video frame packet for a later frame
            _i420aFrameQueue.RecycleStorage(frame);
        }
    }

    private void GetImage() {
        _getimage = false;
        Texture2D tex = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
        RenderTexture.active = renderTexture;
        tex.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);

        //tex = ScaleTexture(tex, renderTexture.width / 2, renderTexture.height / 2);

        tex.Apply();
        var bytes = tex.EncodeToJPG();
        var str = Convert.ToBase64String(bytes);
        UnityEngine.Object.Destroy(tex);
        if (!string.IsNullOrEmpty(str))
        {
            imagestr = str;
        }
    }

    public void TestGet() {
        Debug.Log(imagestr);
    }
}
