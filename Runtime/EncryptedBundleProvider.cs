using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ResourceManagement.Exceptions;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace UTJ
{
    /// <summary>
    /// Provides methods for loading an AssetBundle from a local or remote location.
    /// </summary>
    public class EncryptedBundleResource : IAssetBundleResource
    {
        AssetBundle m_AssetBundle;
        AsyncOperation m_RequestOperation;
        ProvideHandle m_ProvideHandle;

        bool m_RequestCompletedCallbackCalled = false;
        bool m_Completed = false;
        AssetBundleUnloadOperation m_UnloadOperation;
        string m_TransformedInternalId;
        SeekableAesStream m_SeekableStream;
        AssetBundleRequestOptions m_Options;

        public bool isValid => m_Options != null; // m_ProvideHandle.IsValid(); internal only.......

        /// <summary>
        /// Get the asset bundle object managed by this resource.  This call may force the bundle to load if not already loaded.
        /// </summary>
        /// <returns>The asset bundle.</returns>
        public AssetBundle GetAssetBundle() => m_AssetBundle;
        
// NOTE: need to custom BundleAssetProvider.cs
//         AssetBundleRequest m_PreloadRequest;
//         bool m_PreloadCompleted = false;
//
//         /// <summary>
//         /// Creates a request for loading all assets from an AssetBundle.
//         /// </summary>
//         /// <returns>Returns the request.</returns>
//         public AssetBundleRequest GetAssetPreloadRequest()
//         {
//             if (m_PreloadCompleted || GetAssetBundle() == null)
//                 return null;
//
//             if (m_Options.AssetLoadMode == AssetLoadMode.AllPackedAssetsAndDependencies)
//             {
// #if !UNITY_2021_1_OR_NEWER
//                 if (AsyncOperationHandle.IsWaitingForCompletion)
//                 {
//                     m_AssetBundle.LoadAllAssets();
//                     m_PreloadCompleted = true;
//                     return null;
//                 }
// #endif
//                 if (m_PreloadRequest == null)
//                 {
//                     m_PreloadRequest = m_AssetBundle.LoadAllAssetsAsync();
//                     m_PreloadRequest.completed += operation => m_PreloadCompleted = true;
//                 }
//
//                 return m_PreloadRequest;
//             }
//
//             return null;
//         }

        // NOTE: Why using internal API?
        // private void AddBundleToProfiler(Profiling.ContentStatus status, BundleSource source)
        // {
        //     if (!Profiler.enabled)
        //         return;
        //     if (!m_ProvideHandle.IsValid)
        //         return;
        //
        //     if (status == Profiling.ContentStatus.Active && m_AssetBundle == null)
        //         Profiling.ProfilerRuntime.BundleReleased(m_Options.BundleName);
        //     else
        //         Profiling.ProfilerRuntime.AddBundleOperation(m_ProvideHandle, m_Options, status, source);
        // }
        // 
        // private void RemoveBundleFromProfiler()
        // {
        //     if (m_Options == null)
        //         return;
        //     Profiling.ProfilerRuntime.BundleReleased(m_Options.BundleName);
        // }

        // delegate cache
        Action<AsyncOperation> m_WaitForUnloadOperationCompleteHandler;
        Action<AsyncOperation> m_LocalRequestOperationCompletedHandler;
        Func<float> m_PercentCompleteHandler;
        Func<bool> m_WaitForCompletionHandler;
        Action<AsyncOperation> m_UnloadOpCompletedHandler;

        public EncryptedBundleResource()
        {
            // NOTE: create delegate instance
            m_WaitForUnloadOperationCompleteHandler = (op) => {
                m_UnloadOperation = null;
                BeginOperation();
            };
            m_LocalRequestOperationCompletedHandler = LocalRequestOperationCompleted;
            m_PercentCompleteHandler = () => { return m_RequestOperation != null ? m_RequestOperation.progress : 0.0f; };
            m_WaitForCompletionHandler = WaitForCompletion;
            m_UnloadOpCompletedHandler = UnloadOpCompleted;
        }

        /// <summary>
        /// Stores AssetBundle loading information, starts loading the bundle.
        /// </summary>
        /// <param name="provideHandle">The container for AssetBundle loading information.</param>
        public void Start(ProvideHandle provideHandle)
        {
            if (EncryptedBundleProvider.s_UnloadingBundles.TryGetValue(provideHandle.Location.InternalId, out var unloadOp))
            {
                if (unloadOp.isDone)
                    unloadOp = null;
            }

            m_AssetBundle = null;
            m_RequestOperation = null;
            m_RequestCompletedCallbackCalled = false;
            m_ProvideHandle = provideHandle;
            m_Options = m_ProvideHandle.Location.Data as AssetBundleRequestOptions;
            m_TransformedInternalId = m_ProvideHandle.Location.InternalId;
            
            if (m_Options == null)
            {
                m_TransformedInternalId = null;
                m_RequestOperation = null;
                m_ProvideHandle.Complete<EncryptedBundleResource>(null, false,
                    new RemoteProviderException(string.Format("Invalid path in AssetBundleProvider: '{0}'.", m_TransformedInternalId), m_ProvideHandle.Location));
                m_Completed = true;
                return;
            }
            
            m_ProvideHandle.SetProgressCallback(m_PercentCompleteHandler);
            m_ProvideHandle.SetWaitForCompletionCallback(m_WaitForCompletionHandler);
            m_UnloadOperation = unloadOp;
            if (m_UnloadOperation != null && !m_UnloadOperation.isDone)
                m_UnloadOperation.completed += m_WaitForUnloadOperationCompleteHandler;
            else
                BeginOperation();
        }
        
        

        unsafe void BeginOperation()
        {
            //-------------------------------------------------------------------------------
            //m_RequestOperation = AssetBundle.LoadFromFileAsync(m_TransformedInternalId, m_Options == null ? 0 : m_Options.Crc);
            
            // Decrypt
            Span<byte> salt = stackalloc byte[m_Options.BundleName.Length];
            m_Options.BundleName.ConvertTo(salt);
            if (m_SeekableStream == null)
                m_SeekableStream = new SeekableAesStream(m_TransformedInternalId, EncryptedBundleProvider.ENCRYPTED_PASSWORD_BYTES, salt);
            else
                m_SeekableStream.CreateStream(m_TransformedInternalId, EncryptedBundleProvider.ENCRYPTED_PASSWORD_BYTES, salt);
            m_RequestOperation = AssetBundle.LoadFromStreamAsync(m_SeekableStream, 0U, 0U);
            //-------------------------------------------------------------------------------
            
            // NOTE: Why using internal API?
            //AddBundleToProfiler(Profiling.ContentStatus.Loading, BundleSource.Local);
            if (m_RequestOperation.isDone)
                m_LocalRequestOperationCompletedHandler(m_RequestOperation);
            else
                m_RequestOperation.completed += m_LocalRequestOperationCompletedHandler;
        }

        /// <summary>
        /// Starts an async operation that unloads all resources associated with the AssetBundle.
        /// </summary>
        public void Unload(bool async = true)
        {
            if (m_AssetBundle != null)
            {
                if (async)
                {
                    var unloadOp = m_AssetBundle.UnloadAsync(true);
                    unloadOp.completed += m_UnloadOpCompletedHandler;
                }
                else
                {
                    m_AssetBundle.Unload(true);
                    m_UnloadOpCompletedHandler(null);
                }
                m_AssetBundle = null;
            }
            else
            {
                // m_SeekableStream?.Dispose();
                // m_SeekableStream = null;
                m_SeekableStream?.Clear();
                m_TransformedInternalId = null;
            }

            m_RequestOperation = null;
            m_RequestCompletedCallbackCalled = false;
            m_ProvideHandle = default; 
            m_Options = null;
            m_Completed = false;
            m_UnloadOperation = null;
            // NOTE: do not clear until finish to UnloadAsync
            // m_SeekableStream = null;
            // m_TransformedInternalId = null;

            // NOTE: Why using internal API?
            //RemoveBundleFromProfiler();
        }

        public void Dispose()
        {
            // not called on RunTime
            m_SeekableStream?.Dispose();
            m_SeekableStream = null;
        }

        void LocalRequestOperationCompleted(AsyncOperation op)
        {
            if (m_RequestCompletedCallbackCalled)
                return;

            m_RequestCompletedCallbackCalled = true;
            if (op is AssetBundleCreateRequest createReq)
                m_AssetBundle = createReq.assetBundle; // NOTE: sync main-thread if not completed to load
            // NOTE: Why using internal API?
            //AddBundleToProfiler(Profiling.ContentStatus.Active, BundleSource.Local);

            if (m_AssetBundle != null)
                m_ProvideHandle.Complete(this, true, null);
            else
                Debug.LogError(string.Format("Invalid path in EncryptedAssetBundleProvider: '{0}'.", m_TransformedInternalId));
            m_Completed = true;
        }

        bool WaitForCompletion()
        {
            if (m_UnloadOperation != null && !m_UnloadOperation.isDone)
            {
                m_UnloadOperation.completed -= m_WaitForUnloadOperationCompleteHandler;
                m_UnloadOperation.WaitForCompletion();
                m_WaitForUnloadOperationCompleteHandler(m_UnloadOperation);
            }

            if (m_RequestOperation == null)
                return false;

            //if (!m_Completed && m_Source == BundleSource.Local)
            if (!m_Completed)
            {
                // we don't have to check for done with local files as calling
                // m_requestOperation.assetBundle is blocking and will wait for the file to load
                if (!m_RequestCompletedCallbackCalled)
                {
                    m_RequestOperation.completed -= m_LocalRequestOperationCompletedHandler;
                    m_LocalRequestOperationCompletedHandler(m_RequestOperation); // NOTE: m_Completed = true
                }
            }

            // NOTE: In what cases does it reach here?
            if (!m_Completed && m_RequestOperation.isDone)
            {
                m_ProvideHandle.Complete(this, m_AssetBundle != null, null);
                m_Completed = true;
            }

            return m_Completed;
        }

        void UnloadOpCompleted(AsyncOperation op)
        {
#if UNITY_EDITOR
            // NOTE: sync-unloaded when Editor is stopped
            if (op == null)
                Debug.LogWarning($"Unloaded {m_TransformedInternalId}");
#endif
            EncryptedBundleProvider.s_UnloadingBundles.Remove(m_TransformedInternalId);
            // m_SeekableStream.Dispose();
            // m_SeekableStream = null;
            m_SeekableStream.Clear();
            m_TransformedInternalId = null;
        }
    }
    
    /// <summary>
    /// IResourceProvider for asset bundles.  Loads bundles via UnityWebRequestAssetBundle API if the internalId starts with "http".  If not, it will load the bundle via AssetBundle.LoadFromFileAsync.
    /// </summary>
    [System.ComponentModel.DisplayName("Encrypted AssetBundle Provider")]
    public class EncryptedBundleProvider : ResourceProviderBase
    {
        internal const int POOL_CAPACITY = 2; // NOTE: Maximum loaded bundles on runtime
        internal static Stack<EncryptedBundleResource> s_ResourcePool = null;
        internal static Dictionary<string, EncryptedBundleResource> s_ActiveResources = null;
        internal static Dictionary<string, AssetBundleUnloadOperation> s_UnloadingBundles = null;

        internal static byte[] ENCRYPTED_PASSWORD_BYTES = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            s_ActiveResources = new(POOL_CAPACITY);
            s_ResourcePool = new(POOL_CAPACITY);
            s_UnloadingBundles = new(POOL_CAPACITY);
            for (var i = 0; i < POOL_CAPACITY; i++)
                s_ResourcePool.Push(new EncryptedBundleResource());
            ENCRYPTED_PASSWORD_BYTES = System.Text.Encoding.UTF8.GetBytes(Application.unityVersion);
            
            Debug.Log("Create static buffers in EncryptedBundleProvider");
        }

        ~EncryptedBundleProvider()
        {
            Debug.LogError("EncryptedBundleProvider is released");
        }

        /// <inheritdoc/>
        public override void Provide(ProvideHandle providerInterface)
        {
            EncryptedBundleResource res = null;
            if (s_ResourcePool.Count > 0)
            {
                res = s_ResourcePool.Pop();
            }
            else
            {
                Debug.LogWarning("new IAssetBundleResource instance ********************");
                res = new EncryptedBundleResource();
            }

            res.Start(providerInterface);
            s_ActiveResources.Add(providerInterface.Location.InternalId, res);
        }

        /// <inheritdoc/>
        public override Type GetDefaultType(IResourceLocation location)
        {
            return typeof(IAssetBundleResource);
        }

        /// <summary>
        /// Releases the asset bundle via AssetBundle.Unload(true).
        /// </summary>
        /// <param name="location">The location of the asset to release</param>
        /// <param name="asset">The asset in question</param>
        public override void Release(IResourceLocation location, object asset)
        {
#if DEBUG
            if (location == null)
                throw new ArgumentNullException("location");
            if (asset == null || !(asset is EncryptedBundleResource))
            {
                Debug.LogWarningFormat("Releasing null asset bundle from location {0}.  This is an indication that the bundle failed to load.", location);
                return;
            }
#endif
            var bundleRes = asset as EncryptedBundleResource;
            bundleRes.Unload();
            s_ActiveResources.Remove(location.InternalId);
            s_ResourcePool.Push(bundleRes);
        }
    }
    
    public static class StringExtensions {
        public static unsafe void ConvertTo(this string source, Span<byte> dest)
        {
            ReadOnlySpan<char> s = source.AsSpan();
            for (var i = 0; i < s.Length; i++)
                dest[i] = (byte)s[i];
        }
    }
}
