using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ResourceManagement.Exceptions;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace AddrAuditor
{
    /// <summary>
    /// Provides methods for loading an AssetBundle from a local or remote location.
    /// </summary>
    public class LocalAssetBundleResource : IAssetBundleResource
    {
        AssetBundle m_AssetBundle;
        AsyncOperation m_RequestOperation;
        internal ProvideHandle m_ProvideHandle;
        internal AssetBundleRequestOptions m_Options;

        [NonSerialized] bool m_RequestCompletedCallbackCalled = false;

        bool m_Completed = false;
        AssetBundleUnloadOperation m_UnloadOperation;
        string m_TransformedInternalId;
        AssetBundleRequest m_PreloadRequest;
        bool m_PreloadCompleted = false;
        
        // delegate cache
        Action<AsyncOperation> m_WaitForUnloadOperationCompleteHandler;
        Action<AsyncOperation> m_RequestOperationCompletedHandler;
        Action<AsyncOperation> m_PreloadCompletedHandler;
        Func<float> m_PercentCompleteHandler;
        Func<bool> m_WaitForCompletionHandler;

        public LocalAssetBundleResource()
        {
            // NOTE: create delegate instance
            m_WaitForUnloadOperationCompleteHandler = (op) => {
                m_UnloadOperation = null;
                BeginOperation();
            };
            m_RequestOperationCompletedHandler = RequestOperationCompleted;
            m_PreloadCompletedHandler = (op) => m_PreloadCompleted = true;
            m_PercentCompleteHandler = () => m_RequestOperation?.progress ?? 0f;
            m_WaitForCompletionHandler = WaitForCompletion;
        }

        /// <summary>
        /// Creates a request for loading all assets from an AssetBundle.
        /// </summary>
        /// <returns>Returns the request.</returns>
        public AssetBundleRequest GetAssetPreloadRequest()
        {
            if (m_PreloadCompleted || GetAssetBundle() == null)
                return null;
            
            if (m_Options.AssetLoadMode != AssetLoadMode.AllPackedAssetsAndDependencies)
                return null;

            if (m_PreloadRequest != null)
                return m_PreloadRequest;

            m_PreloadRequest = m_AssetBundle.LoadAllAssetsAsync();
            m_PreloadRequest.completed += m_PreloadCompletedHandler;

            return m_PreloadRequest;
        }

        /// <summary>
        /// Get the asset bundle object managed by this resource.  This call may force the bundle to load if not already loaded.
        /// </summary>
        /// <returns>The asset bundle.</returns>
        public AssetBundle GetAssetBundle() => m_AssetBundle;

        // NOTE: These are using internal API, I should throw feedback.
        // private void AddBundleToProfiler(Profiling.ContentStatus status, BundleSource source)
        // {
        //     if (!Profiler.enabled)
        //         return;
        //     if (!m_ProvideHandle.IsValid)
        //         return;
        //
        //     if (status == Profiling.ContentStatus.Active && m_AssetBundle == null) // is this going to suggest load only are released?
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

        /// <summary>
        /// Stores AssetBundle loading information, starts loading the bundle.
        /// </summary>
        /// <param name="provideHandle">The container for AssetBundle loading information.</param>
        /// <param name="unloadOp">The async operation for unloading the AssetBundle.</param>
        public void Start(ProvideHandle provideHandle, AssetBundleUnloadOperation unloadOp)
        {
            m_AssetBundle = null;
            m_RequestOperation = null;
            m_RequestCompletedCallbackCalled = false;
            m_ProvideHandle = provideHandle;
            m_Options = m_ProvideHandle.Location.Data as AssetBundleRequestOptions;
            m_ProvideHandle.SetProgressCallback(m_PercentCompleteHandler);
            m_ProvideHandle.SetWaitForCompletionCallback(this.m_WaitForCompletionHandler);
            m_UnloadOperation = unloadOp;
            if (m_UnloadOperation != null && !m_UnloadOperation.isDone)
                m_UnloadOperation.completed += m_WaitForUnloadOperationCompleteHandler;
            else
                BeginOperation();
        }

        private bool WaitForCompletion()
        {
            if (m_UnloadOperation is { isDone: false })
            {
                m_UnloadOperation.completed -= m_WaitForUnloadOperationCompleteHandler;
                m_UnloadOperation.WaitForCompletion();
                m_UnloadOperation = null;
                BeginOperation();
            }

            if (m_RequestOperation == null)
                return false;

            if (m_Completed)
                return true;
            
            // we don't have to check for done with local files as calling
            // m_requestOperation.assetBundle is blocking and will wait for the file to load
            if (!m_RequestCompletedCallbackCalled)
            {
                m_RequestOperation.completed -= m_RequestOperationCompletedHandler;
                m_RequestOperationCompletedHandler(m_RequestOperation);
            }

            if (m_RequestOperation.isDone)
            {
                m_ProvideHandle.Complete(this, m_AssetBundle != null, null);
                m_Completed = true;
            }

            return m_Completed;
        }

        private void BeginOperation()
        {
            // retrying a failed request will call BeginOperation multiple times. Any member variables
            // should be reset at the beginning of the operation
            m_RequestCompletedCallbackCalled = false;
            
            Debug.Assert(m_ProvideHandle.Location?.Data is not AssetBundleRequestOptions, "Invalid RequestOptions--------------------------");

            m_TransformedInternalId = m_ProvideHandle.ResourceManager.TransformInternalId(m_ProvideHandle.Location);
            
            Debug.Assert(m_Options.Crc == 0, "Should not check CRC for local bundles");

            m_RequestOperation = AssetBundle.LoadFromFileAsync(m_TransformedInternalId, 0U, 0UL);
            //AddBundleToProfiler(Profiling.ContentStatus.Loading, m_Source);
            if (m_RequestOperation.isDone)
                m_RequestOperationCompletedHandler(m_RequestOperation);
            else
                m_RequestOperation.completed += m_RequestOperationCompletedHandler;
        }

        private void RequestOperationCompleted(AsyncOperation op)
        {
            if (m_RequestCompletedCallbackCalled)
                return;

            m_RequestCompletedCallbackCalled = true;

            if (op is not AssetBundleCreateRequest req)
                return;
            
            m_AssetBundle = req.assetBundle;
            //AddBundleToProfiler(Profiling.ContentStatus.Active, m_Source);

            // TODO: whether need to check null
            if (m_AssetBundle != null)
                m_ProvideHandle.Complete(this, true, null);
            else
                m_ProvideHandle.Complete<LocalAssetBundleResource>(null, false,
                    new RemoteProviderException($"Invalid path in LocalAssetBundleProvider: '{m_TransformedInternalId}'.", m_ProvideHandle.Location));
            m_Completed = true;
        }

        /// <summary>
        /// Starts an async operation that unloads all resources associated with the AssetBundle.
        /// </summary>
        /// <param name="unloadOp">The async operation.</param>
        /// <returns>Returns true if the async operation object is valid.</returns>
        public bool Unload(out AssetBundleUnloadOperation unloadOp)
        {
            unloadOp = m_AssetBundle?.UnloadAsync(true);

            m_AssetBundle = null;
            m_RequestOperation = null;
            //RemoveBundleFromProfiler();

            return unloadOp != null;
        }
    }

    /// <summary>
    /// IResourceProvider for asset bundles.
    /// </summary>
    [System.ComponentModel.DisplayName("Local-AssetBundle Provider")]
    public class LocalAssetBundleProvider : ResourceProviderBase
    {
        internal const int POOL_CAPACITY = 8; // NOTE: Maximum loaded bundles on runtime
        internal static Stack<LocalAssetBundleResource> s_ResourcePool = null;
        internal static Dictionary<string, LocalAssetBundleResource> s_ActiveResources = null;
        internal static Dictionary<string, AssetBundleUnloadOperation> s_UnloadingBundles = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            s_ActiveResources = new(POOL_CAPACITY);
            s_ResourcePool = new(POOL_CAPACITY);
            s_UnloadingBundles = new(POOL_CAPACITY);
            for (var i = 0; i < POOL_CAPACITY; i++)
                s_ResourcePool.Push(new ());
            
#if UNITY_EDITOR
            Debug.Log("Create static buffers in EncryptedBundleProvider");
#endif
        }

#if UNITY_EDITOR
        ~LocalAssetBundleProvider()
        {
            Debug.Log("LocalAssetBundleProvider is released");
        }
#endif

        /// <inheritdoc/>
        public override void Provide(ProvideHandle providerInterface)
        {
            if (s_UnloadingBundles.TryGetValue(providerInterface.Location.InternalId, out var unloadOp))
            {
                if (unloadOp.isDone)
                    unloadOp = null;
            }
            
            if (!s_ResourcePool.TryPop(out var res))
            {
                Debug.LogError("overflow LocalAssetBundleResource instance ********************");
                res = new LocalAssetBundleResource();
            }
            res.Start(providerInterface, unloadOp);
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
            if (asset == null)
            {
                Debug.LogWarning(
                    $"Releasing null asset bundle from location {location}.  This is an indication that the bundle failed to load.");
                return;
            }
#endif

            if (asset is not LocalAssetBundleResource bundle)
                return;
            
            if (bundle.Unload(out var unloadOp))
            {
                s_UnloadingBundles.Add(location.InternalId, unloadOp);
                // TODO: delegate instance
                unloadOp.completed += op =>
                {
                    s_UnloadingBundles.Remove(location.InternalId);
                    s_ActiveResources.Remove(location.InternalId);
                    s_ResourcePool.Push(bundle);
                };
            }
        }
    }
}
