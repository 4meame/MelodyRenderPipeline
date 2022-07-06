using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Melody.Cloud {
    public class SharedProperties {
        //atmosphere variables
        Vector3 _earthCenter;
        public Vector3 earthCenter
        {
            get {
                return _earthCenter;
            } set {
                _earthCenter = value;
            }
        }
        float _earthRadius;
        public float earthRadius {
            get {
                return _earthRadius;
            }
            set {
                _earthRadius = value;
                _maxDistance = CalculateMaxDistance();
                _maxRayDistance = CalculateMaxRayDistance();
            }
        }
        float _atmosphereStartHeight;
        public float atmosphereStartHeight {
            get { return _atmosphereStartHeight; }
            set {
                _atmosphereStartHeight = value;
                _maxDistance = CalculateMaxDistance();
                _maxRayDistance = CalculateMaxRayDistance();
            }
        }
        float _atmosphereEndHeight;
        public float atmosphereEndHeight {
            get { return _atmosphereEndHeight; }
            set {
                _atmosphereEndHeight = value;
                _maxDistance = CalculateMaxDistance();
                _maxRayDistance = CalculateMaxRayDistance();
            }
        }
        //camera variables
        public Vector3 cameraPosition;
        float _maxDistance;
        public float maxDistance {
            get {
                return _maxDistance;
            }
        }
        float _maxRayDistance;
        public float maxRayDistance {
            get {
                return _maxRayDistance;
            }
        }
        public Matrix4x4 jitter;
        public Matrix4x4 previousProjection;
        public Matrix4x4 previousInverseRotation;
        public Matrix4x4 previousRotation;
        public Matrix4x4 projection;
        public Matrix4x4 inverseRotation;
        public Matrix4x4 rotation;
        //sub frame variables
        int _subFrameNumber;
        public int subFrameNumber {
            get {
                return _subFrameNumber;
            }
        }
        int _subFrameWidth = 1;
        public int subFrameWidth {
            get {
                return _subFrameWidth;
            }
        }
        int _subFrameHeight = 1;
        public int subFrameHeight {
            get {
                return _subFrameHeight;
            }
        }
        //frame variables
        int _frameWidth = 1;
        public int frameWidth {
            get {
                return _frameWidth;
            }
        }
        int _frameHeight = 1;
        public int frameHeight {
            get {
                return _frameHeight;
            }
        }

        public bool useFixedSizes;
        public int fixedWidth;
        public int fixedHeight;
        bool _sizesChangedSinceLastFrame;
        public bool sizesChangedSinceLastFrame {
            get {
                return _sizesChangedSinceLastFrame;
            }
        }
        int[] _frameNumbers;
        int _renderCount;
        int _downsample;
        public int downsample {
            get { return _downsample; }
            set {
                _downsample = value;
            }
        }
        private int _subPixelSize;
        public int subPixelSize {
            get { return _subPixelSize; }
            set {
                _subPixelSize = value;
                _frameNumbers = CreateFrameNumbers(_subPixelSize);
                _subFrameNumber = 0;
            }
        }

        public SharedProperties() {
            _renderCount = 0;
            downsample = 2;
            subPixelSize = 2;
        }

        int[] CreateFrameNumbers(int subPixelSize) {
            int frameCount = subPixelSize * subPixelSize;
            int i = 0;
            int[] frameNumbers = new int[frameCount];

            for (i = 0; i < frameCount; i++) {
                frameNumbers[i] = i;
            }

            while (i-- > 0) {
                int k = frameNumbers[i];
                int j = (int)(Random.value * 1000.0f) % frameCount;
                frameNumbers[i] = frameNumbers[j];
                frameNumbers[j] = k;
            }

            return frameNumbers;
        }

        public void BeginFrame(Camera camera) {
            UpdateFrameSizes(camera);
            projection = camera.projectionMatrix;
            rotation = camera.worldToCameraMatrix;
            inverseRotation = camera.cameraToWorldMatrix;
            jitter = CreateJitterMatrix();
        }

        public void EndFrame() {
            previousProjection = projection;
            previousRotation = rotation;
            previousInverseRotation = inverseRotation;
            _sizesChangedSinceLastFrame = false;
            _renderCount++;
            _subFrameNumber = _frameNumbers[_renderCount % (subPixelSize * subPixelSize)];
        }

        private void UpdateFrameSizes(Camera camera) {
            int newFrameWidth = useFixedSizes ? fixedWidth : camera.pixelWidth / downsample;
            int newFrameHeight = useFixedSizes ? fixedHeight : camera.pixelHeight / downsample;

            while ((newFrameWidth % _subPixelSize) != 0) {
                newFrameWidth++;
            }
            while ((newFrameHeight % _subPixelSize) != 0) {
                newFrameHeight++;
            }

            int newSubFrameWidth = newFrameWidth / _subPixelSize;
            int newSubFrameHeight = newFrameHeight / _subPixelSize;

            _sizesChangedSinceLastFrame = newFrameWidth != _frameWidth ||
                                          newFrameHeight != _frameHeight ||
                                          newSubFrameWidth != _subFrameWidth ||
                                          newSubFrameHeight != _subFrameHeight;

            _frameWidth = newFrameWidth;
            _frameHeight = newFrameHeight;
            _subFrameWidth = newSubFrameWidth;
            _subFrameHeight = newSubFrameHeight;
        }

        Matrix4x4 CreateJitterMatrix() {
            int x = subFrameNumber % subPixelSize;
            int y = subFrameNumber / subPixelSize;
            Vector3 jitter = new Vector3(x * 2.0f / _frameWidth, y * 2.0f / _frameHeight);
            return Matrix4x4.TRS(jitter, Quaternion.identity, Vector3.one);
        }

        public Vector3 NormalizedPointToAtmosphere(Vector2 point, Camera camera) {
            point.x *= camera.pixelWidth;
            point.y *= camera.pixelHeight;
            return ScreenPointToAtmosphere(point, camera);
        }

        Vector3 ScreenPointToAtmosphere(Vector2 screenPoint, Camera camera) {
            Vector3 atmospherePoint = new Vector3();
            Vector2 uv = new Vector2(screenPoint.x / camera.pixelWidth, screenPoint.y / camera.pixelHeight);
            Vector4 screenRay = new Vector4(uv.x * 2.0f - 1.0f, uv.y * 2.0f - 1.0f, 1.0f, 1.0f);
            screenRay = camera.projectionMatrix.inverse * screenRay;
            screenRay.x /= screenRay.w;
            screenRay.y /= screenRay.w;
            screenRay.z /= screenRay.w;
            Vector3 rayDirection = new Vector3(screenRay.x, screenRay.y, screenRay.z);
            rayDirection = camera.cameraToWorldMatrix.MultiplyVector(rayDirection).normalized;

            float radius = earthRadius + atmosphereStartHeight;
            atmospherePoint = InternalRaySphereIntersect(radius, cameraPosition, rayDirection);
            return atmospherePoint;
        }

        float CalculateHorizontalDistance(float innerRadius, float outerRadius) {
            return Mathf.Sqrt((outerRadius * outerRadius) - (innerRadius * innerRadius));
        }

        public float CalculatePlanetRadius(float atmosphereHeight, float horizonDistance) {
            float atmosphereRadius = atmosphereHeight * atmosphereHeight + horizonDistance * horizonDistance;
            atmosphereRadius /= 2.0f * atmosphereHeight;
            return atmosphereRadius - atmosphereHeight;
        }

        float CalculateMaxDistance() {
            float maxDistance =  CalculateHorizontalDistance(earthRadius, earthRadius + atmosphereEndHeight);
            float h = cameraPosition.y - earthRadius;
            float horizon = Mathf.Sqrt(2.0f * earthRadius * h + h * h);
            //return horizon distance is wrong, cloud coverage is no matter how far eyes look towards
            //return Mathf.Min(maxDistance, horizon);
            return maxDistance;
        }

        float CalculateMaxRayDistance() {
            float cloudInnerDistance = CalculateHorizontalDistance(earthRadius, earthRadius + atmosphereStartHeight);
            float cloudOuterDistance = CalculateHorizontalDistance(earthRadius, earthRadius + atmosphereEndHeight);
            return cloudOuterDistance - cloudInnerDistance;
        }

        Vector3 InternalRaySphereIntersect(float sphereRadius, Vector3 origin, Vector3 rayDirection) {
            float a0 = sphereRadius * sphereRadius - Vector3.Dot(origin, origin);
            float a1 = Vector3.Dot(origin, rayDirection);
            float result = Mathf.Sqrt(a1 * a1 + a0) - a1;
            return origin + rayDirection * result;
        }

        Vector2 RaySphereDst(Vector3 sphereCenter, float sphereRadius, Vector3 origin, Vector3 direction) {
            Vector3 oc = origin - sphereCenter;
            float b = Vector3.Dot(direction, oc);
            float c = Vector3.Dot(oc, oc) - sphereRadius * sphereRadius;
            float t = b * b - c;
            // CASE 1: ray intersects sphere(t > 0)
            // dstA is dst to nearest intersection, dstB dst to far intersection
            // CASE 2: ray touches sphere(t = 0)
            // dstA is the dst to intersection behind the ray, dstB is dst to forward intersection
            // CASE 3: ray misses sphere (t < 0)
            float delta = (float)Mathf.Sqrt(Mathf.Max(t, 0));
            float dstToSphere = Mathf.Max(-b - delta, 0);
            float dstInSphere = Mathf.Max(-b + delta - dstToSphere, 0);
            return new Vector2(dstToSphere, dstInSphere);
        }

        Vector2 RayIntersectCloudDistance(Vector3 sphereCenter, Vector3 origin, Vector3 direction) {
            Vector2 cloudDstMin = RaySphereDst(sphereCenter, atmosphereStartHeight + earthRadius, origin, direction);
            Vector2 cloudDstMax = RaySphereDst(sphereCenter, atmosphereEndHeight + earthRadius, origin, direction);
            float dstToCloud = 0;
            float dstInCloud = 0;
            float d = Vector3.Distance(origin, sphereCenter);
            //on the ground
            if (d <= atmosphereStartHeight + earthRadius) {
                Vector3 startPos = origin + direction * cloudDstMin.y;
                if (startPos.y >= 0) {
                    dstToCloud = cloudDstMin.y;
                    dstInCloud = cloudDstMax.y - cloudDstMin.y;
                }
                return new Vector2(dstToCloud, dstInCloud);
            }
            //in the cloud
            else if (d > atmosphereStartHeight + earthRadius && d <= atmosphereEndHeight + earthRadius) {
                dstToCloud = 0;
                dstInCloud = cloudDstMin.y > 0 ? cloudDstMin.x : cloudDstMax.y;
                return new Vector2(dstToCloud, dstInCloud);
            }
            //outside the cloud
            else {
                dstToCloud = cloudDstMax.x;
                dstInCloud = cloudDstMin.y > 0 ? cloudDstMin.x - dstToCloud : cloudDstMax.y;
            }
            return new Vector2(dstToCloud, dstInCloud);
        }

        public void ApplyToMaterial(Material material, bool jitterProjection = false) {
            Matrix4x4 inverseProjection = projection.inverse;
            if (jitterProjection) {
                inverseProjection *= jitter;
            }
            material.SetVector("_EarthCenter", earthCenter);
            material.SetFloat("_EarthRadius", earthRadius);
            material.SetFloat("_StartHeight", atmosphereStartHeight);
            material.SetFloat("_EndHeight", atmosphereEndHeight);
            material.SetFloat("_AtmosphereThickness", atmosphereEndHeight - atmosphereStartHeight);
            material.SetVector("_CameraPosition", cameraPosition);
            material.SetFloat("_MaxDistance", maxDistance);
            material.SetMatrix("_PreviousProjection", previousProjection);
            material.SetMatrix("_PreviousInverseProjection", previousProjection.inverse);
            material.SetMatrix("_PreviousRotation", previousRotation);
            material.SetMatrix("_PreviousInverseRotation", previousInverseRotation);
            material.SetMatrix("_Projection", projection);
            material.SetMatrix("_InverseProjection", inverseProjection);
            material.SetMatrix("_Rotation", rotation);
            material.SetMatrix("_InverseRotation", inverseRotation);
            material.SetFloat("_SubFrameNumber", subFrameNumber);
            material.SetFloat("_SubPixelSize", subPixelSize);
            material.SetVector("_SubFrameSize", new Vector2(_subFrameWidth, _subFrameHeight));
            material.SetVector("_FrameSize", new Vector2(_frameWidth, _frameHeight));
        }
    }
}
