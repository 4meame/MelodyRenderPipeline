using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Crest
{
    /// <summary>
    /// Circular buffer to store a multiple sets of data 
    /// </summary>
    public class BufferedData<T>
    {
        public BufferedData(int bufferSize, Func<T> initFunc)
        {
            _buffers = new T[bufferSize];
            for (int i = 0; i < bufferSize; i++)
            {
                _buffers[i] = initFunc();
            }
        }

        public T Current { get => _buffers[_currentFrameIndex]; set => _buffers[_currentFrameIndex] = value; }

        public int Size => _buffers.Length;

        public T Previous(int framesBack)
        {
            Debug.Assert(framesBack >= 0 && framesBack < _buffers.Length);
            int index = (_currentFrameIndex - framesBack + _buffers.Length) % _buffers.Length;
            return _buffers[index];
        }

        public void Flip()
        {
            _currentFrameIndex = (_currentFrameIndex + 1) % _buffers.Length;
        }

        public void RunLambda(Action<T> lambda)
        {
            foreach (var buffer in _buffers)
            {
                lambda(buffer);
            }
        }

        T[] _buffers = null;
        int _currentFrameIndex = 0;
    }

    /// <summary>
    /// Base class for data/behaviours created on each LOD.
    /// </summary>
    public abstract class LodDataMgr
    {
        public abstract string SimName { get; }
        // This is the texture format we want to use.
        protected abstract GraphicsFormat RequestedTextureFormat { get; }
        // This is the platform compatible texture format we will use.
        public GraphicsFormat CompatibleTextureFormat { get; private set; }
        // NOTE: This MUST match the value in OceanConstants.hlsl, as it
        // determines the size of the texture arrays in the shaders.
        public const int MAX_LOD_COUNT = 15;
        // NOTE: these MUST match the values in OceanConstants.hlsl
        // 64 recommended as a good common minimum: https://www.reddit.com/r/GraphicsProgramming/comments/aeyfkh/for_compute_shaders_is_there_an_ideal_numthreads/
        public const int THREAD_GROUP_SIZE_X = 8;
        public const int THREAD_GROUP_SIZE_Y = 8;
        // NOTE: This is a temporary solution to keywords having prefixes downstream.
        internal static readonly string MATERIAL_KEYWORD_PREFIX = "";

        protected abstract int GetParamIdSampler(bool sourceLod = false);
        protected abstract bool NeedToReadWriteTextureData { get; }
        protected BufferedData<RenderTexture> _targets;
        public RenderTexture DataTexture => _targets.Current;
        public RenderTexture GetDataTexture(int frameDelta) => _targets.Previous(frameDelta);
        public virtual int BufferCount => 1;
        public virtual void FlipBuffers() => _targets.Flip();

        protected virtual Texture2DArray NullTexture => TextureArrayHelpers.BlackTextureArray;

        public static int sp_LD_SliceIndex = Shader.PropertyToID("_LD_SliceIndex");
        protected static int sp_LODChange = Shader.PropertyToID("_LODChange");

        protected virtual int ResolutionOverride => -1;

        // shape texture resolution
        int _shapeRes = -1;

        public bool enabled { get; protected set; }

        protected OceanRenderer _ocean;
    }
}
