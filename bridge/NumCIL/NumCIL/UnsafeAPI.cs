﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using NumCIL.Generic;

namespace NumCIL
{
    /// <summary>
    /// This class wraps access to the unsafe methods
    /// </summary>
    public static class UnsafeAPI
    {
        /// <summary>
        /// Gets or sets a value indicating if use of unsafe methods is disabled
        /// </summary>
        public static bool DisableUnsafeAPI { get; set; }
        /// <summary>
        /// Gets a value indicating if unsafe operations are supported by the runtime/environment
        /// </summary>
        public static readonly bool IsUnsafeSupported;
        /// <summary>
        /// The method used to retrieve binary apply methods
        /// </summary>
        private static readonly MethodInfo _getBinaryApply;
        /// <summary>
        /// The method used to retrieve unary apply methods
        /// </summary>
        private static readonly MethodInfo _getUnaryApply;
        /// <summary>
        /// The method used to retrieve nullary apply methods
        /// </summary>
        private static readonly MethodInfo _getNullaryApply;
        /// <summary>
        /// The method used to retrieve aggregate methods
        /// </summary>
        private static readonly MethodInfo _getAggregate;
        /// <summary>
        /// The method used to retrieve reduce methods
        /// </summary>
        private static readonly MethodInfo _getReduce;
        /// <summary>
        /// The method used to retrieve copyToManaged methods
        /// </summary>
        private static readonly MethodInfo _getCopyToManaged;
        /// <summary>
        /// The method used to retrieve copyToManaged methods
        /// </summary>
        private static readonly MethodInfo _getCopyFromManaged;
        /// <summary>
        /// The method used to retrieve the createAccessor methods
        /// </summary>
        private static readonly MethodInfo _getCreateAccessorSize;
        /// <summary>
        /// The method used to retrieve the createAccessor methods
        /// </summary>
        private static readonly MethodInfo _getCreateAccessorData;

        /// <summary>
        /// Cache of compiled unsafe binary methods bound to a particular operator
        /// </summary>
        private static readonly Dictionary<object, MethodInfo> _binaryMethodCache = new Dictionary<object, MethodInfo>();
        /// <summary>
        /// Cache of compiled unsafe unary methods bound to a particular operator
        /// </summary>
        private static readonly Dictionary<object, MethodInfo> _unaryMethodCache = new Dictionary<object, MethodInfo>();
        /// <summary>
        /// Cache of compiled unsafe nullary methods bound to a particular operator
        /// </summary>
        private static readonly Dictionary<object, MethodInfo> _nullaryMethodCache = new Dictionary<object, MethodInfo>();
        /// <summary>
        /// Cache of compiled unsafe aggregate methods bound to a particular operator
        /// </summary>
        private static readonly Dictionary<object, MethodInfo> _aggregateMethodCache = new Dictionary<object, MethodInfo>();
        /// <summary>
        /// Cache of compiled unsafe reduce methods bound to a particular operator
        /// </summary>
        private static readonly Dictionary<object, MethodInfo> _reduceMethodCache = new Dictionary<object, MethodInfo>();
        /// <summary>
        /// Cache of compiled unsafe copy methods bound to a particular type
        /// </summary>
        private static readonly Dictionary<Type, MethodInfo> _copyToManagedMethodCache = new Dictionary<Type, MethodInfo>();
        /// <summary>
        /// Cache of compiled unsafe copy methods bound to a particular type
        /// </summary>
        private static readonly Dictionary<Type, MethodInfo> _copyFromManagedMethodCache = new Dictionary<Type, MethodInfo>();
        /// <summary>
        /// Cache of compiled unsafe copy methods bound to a particular type
        /// </summary>
        private static readonly Dictionary<Type, MethodInfo> _createAccessorSizeMethodCache = new Dictionary<Type, MethodInfo>();
        /// <summary>
        /// Cache of compiled unsafe copy methods bound to a particular type
        /// </summary>
        private static readonly Dictionary<Type, MethodInfo> _createAccessorDataMethodCache = new Dictionary<Type, MethodInfo>();

        /// <summary>
        /// Static constructor, loads the unsafe dll and probes for support,
        /// and sets up the accessor methods
        /// </summary>
        static UnsafeAPI()
        {
            bool unsafeSupported = false;
            MethodInfo supportsBinaryApply = null;
            MethodInfo supportsUnaryApply = null;
            MethodInfo supportsNullaryApply = null;
            MethodInfo supportsAggregate = null;
            MethodInfo supportsReduce = null;
            MethodInfo supportsCopyToManaged = null;
            MethodInfo supportsCopyFromManaged = null;
            MethodInfo supportsCreateAccessorSize = null;
            MethodInfo supportsCreateAccessorData = null;

            try
            {
                string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "NumCIL.Unsafe.dll");
                Assembly asm = Assembly.LoadFrom(path);
                Type utilityType = asm.GetType("NumCIL.Unsafe.Utility");
                unsafeSupported = (bool)utilityType.GetProperty("SupportsUnsafe").GetValue(null, null);

                if (unsafeSupported)
                {
                    supportsBinaryApply = utilityType.GetMethod("GetBinaryApply");
                    supportsUnaryApply = utilityType.GetMethod("GetUnaryApply");
                    supportsNullaryApply = utilityType.GetMethod("GetNullaryApply");
                    supportsAggregate = utilityType.GetMethod("GetAggregate");
                    supportsReduce = utilityType.GetMethod("GetReduce");
                    supportsCopyToManaged = utilityType.GetMethod("GetCopyToManaged");
                    supportsCopyFromManaged = utilityType.GetMethod("GetCopyFromManaged");
                    supportsCreateAccessorSize = utilityType.GetMethod("GetCreateAccessorSize");
                    supportsCreateAccessorData = utilityType.GetMethod("GetCreateAccessorData");
                    unsafeSupported &= supportsBinaryApply != null;
                    unsafeSupported &= supportsUnaryApply != null;
                    unsafeSupported &= supportsNullaryApply != null;
                    unsafeSupported &= supportsAggregate != null;
                    unsafeSupported &= supportsReduce != null;
                    unsafeSupported &= supportsCreateAccessorSize != null;
                    unsafeSupported &= supportsCreateAccessorData != null;
                }
            }
            catch
            {
                unsafeSupported = false;
            }

            IsUnsafeSupported = unsafeSupported;

            if (IsUnsafeSupported)
            {
                _getBinaryApply = supportsBinaryApply;
                _getUnaryApply = supportsUnaryApply;
                _getNullaryApply = supportsNullaryApply;
                _getAggregate = supportsAggregate;
                _getReduce = supportsReduce;
                _getCopyFromManaged = supportsCopyFromManaged;
                _getCopyToManaged = supportsCopyToManaged;
                _getCreateAccessorSize = supportsCreateAccessorSize;
                _getCreateAccessorData = supportsCreateAccessorData;
            }
            else
            {
                _getBinaryApply = null;
                _getUnaryApply = null;
                _getNullaryApply = null;
                _getAggregate = null;
                _getReduce = null;
                _getCopyFromManaged = null;
                _getCopyToManaged = null;
                _getCreateAccessorSize = null;
                _getCreateAccessorData = null;
            }

            if (Environment.GetEnvironmentVariable("NUMCIL_DISABLE_UNSAFE") != null)
                DisableUnsafeAPI = true;
        }

        /// <summary>
        /// Attempts to load an unsafe method, compile it for the given type and operator and then execute it with the gicen operands
        /// </summary>
        /// <typeparam name="T">The type of data to operate on</typeparam>
        /// <typeparam name="C">The type of operation to perform</typeparam>
        /// <param name="op">The operation instance</param>
        /// <param name="in1">The left-hand-side argument</param>
        /// <param name="in2">The right-handside argument</param>
        /// <param name="out">The output target</param>
        /// <returns>True if the operation was supported and executed, false otherwise</returns>
        internal static bool UFunc_Op_Inner_Binary_Flush_Unsafe<T, C>(C op, NdArray<T> in1, NdArray<T> in2, ref NdArray<T> @out)
            where C : struct, IBinaryOp<T>
        {
            if (DisableUnsafeAPI || !IsUnsafeSupported)
                return false;

            MethodInfo mi;
            if (!_binaryMethodCache.TryGetValue(op, out mi))
            {
                MethodInfo mix = (MethodInfo)_getBinaryApply.MakeGenericMethod(typeof(T)).Invoke(null, null);
                if (mix != null)
                    mi = mix.MakeGenericMethod(typeof(C));
                
                _binaryMethodCache[op] = mi;
            }

            if (mi == null)
                return false;

            mi.Invoke(null, new object[] { op, in1, in2, @out });
            return true;
        }

        /// <summary>
        /// Attempts to load an unsafe method, compile it for the given type and operator and then execute it with the gicen operands
        /// </summary>
        /// <typeparam name="T">The type of data to operate on</typeparam>
        /// <typeparam name="C">The type of operation to perform</typeparam>
        /// <param name="op">The operation instance</param>
        /// <param name="in1">The input argument</param>
        /// <param name="out">The output target</param>
        /// <returns>True if the operation was supported and executed, false otherwise</returns>
        internal static bool UFunc_Op_Inner_Unary_Flush_Unsafe<T, C>(C op, NdArray<T> in1, ref NdArray<T> @out)
            where C : struct, IUnaryOp<T>
        {
            if (DisableUnsafeAPI || !IsUnsafeSupported)
                return false;

            MethodInfo mi;
            if (!_unaryMethodCache.TryGetValue(op, out mi))
            {
                MethodInfo mix = (MethodInfo)_getUnaryApply.MakeGenericMethod(typeof(T)).Invoke(null, null);
                if (mix != null)
                    mi = mix.MakeGenericMethod(typeof(C));

                _unaryMethodCache[op] = mi;
            }

            if (mi == null)
                return false;

            mi.Invoke(null, new object[] { op, in1, @out });
            return true;
        }

        /// <summary>
        /// Attempts to load an unsafe method, compile it for the given type and operator and then execute it with the gicen operands
        /// </summary>
        /// <typeparam name="T">The type of data to operate on</typeparam>
        /// <typeparam name="C">The type of operation to perform</typeparam>
        /// <param name="op">The operation instance</param>
        /// <param name="out">The output target</param>
        /// <returns>True if the operation was supported and executed, false otherwise</returns>
        internal static bool UFunc_Op_Inner_Nullary_Flush_Unsafe<T, C>(C op, NdArray<T> @out)
            where C : struct, INullaryOp<T>
        {
            if (DisableUnsafeAPI || !IsUnsafeSupported)
                return false;

            MethodInfo mi;
            if (!_nullaryMethodCache.TryGetValue(op, out mi))
            {
                MethodInfo mix = (MethodInfo)_getNullaryApply.MakeGenericMethod(typeof(T)).Invoke(null, null);
                if (mix != null)
                    mi = mix.MakeGenericMethod(typeof(C));

                _nullaryMethodCache[op] = mi;
            }

            if (mi == null)
                return false;

            mi.Invoke(null, new object[] { op, @out });
            return true;
        }

        /// <summary>
        /// Attempts to load an unsafe method, compile it for the given type and operator and then execute it with the gicen operands
        /// </summary>
        /// <typeparam name="T">The type of data to operate on</typeparam>
        /// <typeparam name="C">The type of operation to perform</typeparam>
        /// <param name="op">The operation instance</param>
        /// <param name="in1">The left-hand-side argument</param>
        /// <param name="res">The output result</param>
        /// <returns>True if the operation was supported and executed, false otherwise</returns>
        internal static bool Aggregate_Entry_Unsafe<T, C>(C op, NdArray<T> in1, out T res)
            where C : struct, IBinaryOp<T>
        {
            if (DisableUnsafeAPI || !IsUnsafeSupported)
            {
                res = default(T);
                return false;
            }

            MethodInfo mi;
            if (!_aggregateMethodCache.TryGetValue(op, out mi))
            {
                MethodInfo mix = (MethodInfo)_getAggregate.MakeGenericMethod(typeof(T)).Invoke(null, null);
                if (mix != null)
                    mi = mix.MakeGenericMethod(typeof(C));

                _aggregateMethodCache[op] = mi;
            }

            if (mi == null)
            {
                res = default(T);
                return false;
            }

            res = (T)mi.Invoke(null, new object[] { op, in1 });
            return true;
        }

        /// <summary>
        /// Attempts to load an unsafe method, compile it for the given type and operator and then execute it with the gicen operands
        /// </summary>
        /// <typeparam name="T">The type of data to operate on</typeparam>
        /// <typeparam name="C">The type of operation to perform</typeparam>
        /// <param name="op">The operation instance</param>
        /// <param name="in1">The left-hand-side argument</param>
        /// <param name="axis">The axis to reduce over</param>
        /// <param name="out">The output result</param>
        /// <returns>True if the operation was supported and executed, false otherwise</returns>
        internal static bool UFunc_Reduce_Inner_Flush_Unsafe<T, C>(C op, long axis, NdArray<T> in1, NdArray<T> @out)
            where C : struct, IBinaryOp<T>
        {
            if (DisableUnsafeAPI || !IsUnsafeSupported)
                return false;

            MethodInfo mi;
            if (!_reduceMethodCache.TryGetValue(op, out mi))
            {
                MethodInfo mix = (MethodInfo)_getReduce.MakeGenericMethod(typeof(T)).Invoke(null, null);
                if (mix != null)
                    mi = mix.MakeGenericMethod(typeof(C));

                _reduceMethodCache[op] = mi;
            }

            if (mi == null)
                return false;

            mi.Invoke(null, new object[] { op, axis, in1, @out });
            return true;
        }
        
        /// <summary>
        /// Copies the content of the array to the target memory area.
        /// If the copy succeeds, the return value is true, and false if the copy has not been performed
        /// </summary>
        /// <typeparam name="T">The type of data to process.</typeparam>
        /// <param name="data">The data to copy from</param>
        /// <param name="target">The memory area to copy to</param>
        /// <param name="length">The number of elements to copy</param>
        /// <returns>True if the call succeeded, false otherwise</returns>
        public static bool CopyToIntPtr<T>(T[] data, IntPtr target, long length = -1)
        {
            if (DisableUnsafeAPI || !IsUnsafeSupported)
                return false;

            MethodInfo mi;
            if (!_copyFromManagedMethodCache.TryGetValue(typeof(T), out mi))
            {
                mi = (MethodInfo)_getCopyFromManaged.MakeGenericMethod(typeof(T)).Invoke(null, null);
                _copyFromManagedMethodCache[typeof(T)] = mi;
            }

            if (mi == null)
                return false;

            if (length < 0)
                length = data.LongLength;

            mi.Invoke(null, new object[] { target, data, length });
            return true;
        }

        /// <summary>
        /// Copies the content of the memory area into the array.
        /// If the copy succeeds, the return value is true, and false if the copy has not been performed
        /// </summary>
        /// <typeparam name="T">The type of data to process.</typeparam>
        /// <param name="data">The data to copy to</param>
        /// <param name="source">The memory area to copy from</param>
        /// <param name="length">The number of elements to copy</param>
        /// <returns>True if the call succeeded, false otherwise</returns>
        public static bool CopyFromIntPtr<T>(IntPtr source, T[] data, long length = -1)
        {
            if (DisableUnsafeAPI || !IsUnsafeSupported)
                return false;

            MethodInfo mi;
            if (!_copyToManagedMethodCache.TryGetValue(typeof(T), out mi))
            {
                mi = (MethodInfo)_getCopyToManaged.MakeGenericMethod(typeof(T)).Invoke(null, null);
                _copyToManagedMethodCache[typeof(T)] = mi;
            }

            if (mi == null)
                return false;

            if (length < 0)
                length = data.LongLength;

            mi.Invoke(null, new object[] { data, source, length });
            return true;
        }

        /// <summary>
        /// Creates an unmanaged data accessor
        /// </summary>
        /// <typeparam name="T">The type of data to allocate</typeparam>
        /// <param name="size">The size of the array to allocate</param>
        /// <returns>An accessor for the array</returns>
        public static IDataAccessor<T> CreateAccessor<T>(long size)
        {
            if (DisableUnsafeAPI || !IsUnsafeSupported)
                return null;

            MethodInfo mi;
            if (!_createAccessorSizeMethodCache.TryGetValue(typeof(T), out mi))
            {
                mi = (MethodInfo)_getCreateAccessorSize.MakeGenericMethod(typeof(T)).Invoke(null, null);
                _createAccessorSizeMethodCache[typeof(T)] = mi;
            }

            if (mi == null)
                return null;

            return (IDataAccessor<T>)mi.Invoke(null, new object[] { size });
        }

        /// <summary>
        /// Creates an unmanaged data accessor
        /// </summary>
        /// <typeparam name="T">The type of data to allocate</typeparam>
        /// <param name="data">The data to pre-fill the accessor with</param>
        /// <returns>An accessor for the array</returns>
        public static IDataAccessor<T> CreateAccessor<T>(T[] data)
        {
            if (DisableUnsafeAPI || !IsUnsafeSupported)
                return null;

            MethodInfo mi;
            if (!_createAccessorDataMethodCache.TryGetValue(typeof(T), out mi))
            {
                mi = (MethodInfo)_getCreateAccessorData.MakeGenericMethod(typeof(T)).Invoke(null, null);
                _createAccessorDataMethodCache[typeof(T)] = mi;
            }

            if (mi == null)
                return null;

            return (IDataAccessor<T>)mi.Invoke(null, new object[] { data });
        }
    }
}
