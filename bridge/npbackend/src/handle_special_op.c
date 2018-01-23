/*
This file is part of Bohrium and copyright (c) 2012 the Bohrium
team <http://www.bh107.org>.

Bohrium is free software: you can redistribute it and/or modify
it under the terms of the GNU Lesser General Public License as
published by the Free Software Foundation, either version 3
of the License, or (at your option) any later version.

Bohrium is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the
GNU Lesser General Public License along with Bohrium.

If not, see <http://www.gnu.org/licenses/>.
*/

#include "util.h"
#include <bhc.h>
#include "bharray.h"

PyObject *
PyExtMethod(PyObject *self, PyObject *args, PyObject *kwds) {
    char *name;
    PyObject *operand_fast_seq;
    {
        PyObject *operand_list;
        static char *kwlist[] = {"name", "operand_list:list", NULL};
        if (!PyArg_ParseTupleAndKeywords(args, kwds, "sO", kwlist, &name, &operand_list)) {
            return NULL;
        }
        operand_fast_seq = PySequence_Fast(operand_list, "`operand_list` should be a sequence.");
        if (operand_fast_seq == NULL) {
            return NULL;
        }
    }
    const Py_ssize_t nop = PySequence_Fast_GET_SIZE(operand_fast_seq);
    if (nop != 3) {
        PyErr_Format(PyExc_TypeError, "Expects three operands.");
        Py_DECREF(operand_fast_seq);
        return NULL;
    }

    // Read and normalize all operands
    bhc_dtype types[nop];
    bhc_bool constant;
    void *operands[nop];
    normalize_cleanup_handle cleanup;
    cleanup.objs2free_count = 0;
    for (int i = 0; i < nop; ++i) {
        PyObject *op = PySequence_Fast_GET_ITEM(operand_fast_seq, i); // Borrowed reference and will not fail
        int err = normalize_operand(op, &types[i], &constant, &operands[i], &cleanup);
        if (err != -1) {
            if (constant) {
                PyErr_Format(PyExc_TypeError, "Scalars isn't supported.");
                err = -1;
            } else if (types[0] != types[i]) {
                PyErr_Format(PyExc_TypeError, "The dtype of all operands must be the same.");
                err = -1;
            }
        }
        if (err == -1) {
            normalize_operand_cleanup(&cleanup);
            Py_DECREF(operand_fast_seq);
            if (PyErr_Occurred() != NULL) {
                return NULL;
            } else {
                Py_RETURN_NONE;
            }
        }

    }

    int err = bhc_extmethod(types[0], name, operands[0], operands[1], operands[2]);
    if (err) {
        PyErr_Format(PyExc_TypeError, "The current runtime system does not support the extension method '%s'", name);
    }

    // Clean up
    normalize_operand_cleanup(&cleanup);
    Py_DECREF(operand_fast_seq);

    if (PyErr_Occurred() != NULL) {
        return NULL;
    } else {
        Py_RETURN_NONE;
    }
}

PyObject* PyFlush(PyObject *self, PyObject *args) {
    bhc_flush();
    Py_RETURN_NONE;
}

PyObject* PyFlushCount(PyObject *self, PyObject *args) {
    return PyLong_FromLong(bhc_flush_count());
}

PyObject* PyFlushCountAndRepeat(PyObject *self, PyObject *args, PyObject *kwds) {
    unsigned long nrepeats;
    PyObject *condition = NULL;
    static char *kwlist[] = {"nrepeats", "condition:bharray", NULL};
    if (!PyArg_ParseTupleAndKeywords(args, kwds, "k|O", kwlist, &nrepeats, &condition)) {
        return NULL;
    }
    if (condition == NULL || condition == Py_None) {
        bhc_flush_and_repeat(nrepeats);
    } else if(!BhArray_CheckExact(condition)) {
        PyErr_Format(PyExc_TypeError, "The condition must a bharray array or None");
        return NULL;
    } else {
        bhc_dtype type;
        bhc_bool constant;
        void *operand;
        normalize_cleanup_handle cleanup;
        cleanup.objs2free_count = 0;
        int err = normalize_operand(condition, &type, &constant, &operand, &cleanup);
        if (err == -1) {
            normalize_operand_cleanup(&cleanup);
            if (PyErr_Occurred() != NULL) {
                return NULL;
            } else {
                Py_RETURN_NONE;
            }
        }
        bhc_flush_and_repeat_condition(nrepeats, operand);
        normalize_operand_cleanup(&cleanup);
    }
    Py_RETURN_NONE;
}

PyObject* PySync(PyObject *self, PyObject *args, PyObject *kwds) {
    PyObject *ary;
    static char *kwlist[] = {"ary", NULL};
    if (!PyArg_ParseTupleAndKeywords(args, kwds, "O", kwlist, &ary)) {
        return NULL;
    }

    bhc_dtype type;
    bhc_bool constant;
    void *operand;
    normalize_cleanup_handle cleanup;
    cleanup.objs2free_count = 0;
    int err = normalize_operand(ary, &type, &constant, &operand, &cleanup);
    if (err != -1) {
        normalize_operand_cleanup(&cleanup);
        if (PyErr_Occurred() != NULL) {
            return NULL;
        } else {
            Py_RETURN_NONE;
        }
    }
    bhc_sync(type, operand);
    normalize_operand_cleanup(&cleanup);
    Py_RETURN_NONE;
}

PyObject* PyRandom123(PyObject *self, PyObject *args, PyObject *kwds) {
    unsigned long long size;
    unsigned long long seed;
    unsigned long long key;
    static char *kwlist[] = {"size:int", "seed:int", "key:int", NULL};
    if (!PyArg_ParseTupleAndKeywords(args, kwds, "KKK", kwlist, &size, &seed, &key)) {
        return NULL;
    }

    npy_intp shape[1] = {size};
    PyObject *ret = simply_new_array(&BhArrayType, PyArray_DescrFromType(NPY_UINT64), size*8, 1, shape);
    if (ret == NULL) {
        return NULL;
    }
    if (size > 0) {
        bhc_dtype type;
        bhc_bool constant;
        void *operand;
        normalize_cleanup_handle cleanup;
        cleanup.objs2free_count = 0;
        int err = normalize_operand(ret, &type, &constant, &operand, &cleanup);
        if (err == -1) {
            normalize_operand_cleanup(&cleanup);
            if (PyErr_Occurred() != NULL) {
                return NULL;
            } else {
                return ret;
            }
        }
        bhc_random123_Auint64_Kuint64_Kuint64(operand, seed, key);
        normalize_operand_cleanup(&cleanup);
    }
    return ret;
}

void *BhGetDataPointer(BhArray *ary, bhc_bool copy2host, bhc_bool force_alloc, bhc_bool nullify) {

    if (PyArray_SIZE((PyArrayObject*) ary) <= 0) {
        return NULL;
    }
/*
    PyObject *bhc_view = PyObject_CallMethod(bhary, "get_bhc", "O", (PyObject*)ary);
    if(bhc_view == NULL) {
        fprintf(stderr, "Fatal error: get_bhc()\n");
        assert(1 == 2);
        exit(-1);
    }
    PyObject *bhc_ary_swig_ptr = PyObject_GetAttrString(bhc_view, "bhc_obj");
    if(bhc_ary_swig_ptr == NULL) {
        fprintf(stderr, "Fatal error: bhc_obj()\n");
        assert(1 == 2);
        exit(-1);
    }
    PyObject *bhc_ary_ptr = PyObject_CallMethod(bhc_ary_swig_ptr, "__int__", NULL);
    if(bhc_ary_ptr == NULL) {
        fprintf(stderr, "Fatal error: __int__()\n");
        assert(1 == 2);
        exit(-1);
    }
    void *ary_ptr = PyLong_AsVoidPtr((PyObject*) bhc_ary_ptr);
*/
    void *ary_ptr = bharray_bhc(ary);
    bhc_dtype dtype = dtype_np2bhc(PyArray_DESCR((PyArrayObject*) ary)->type_num);

    if (copy2host) {
        bhc_sync(dtype, ary_ptr);
    }
    bhc_flush();

    void *ret = bhc_data_get(dtype, ary_ptr, copy2host, force_alloc, nullify);
/*
    Py_DECREF(bhc_ary_ptr);
    Py_DECREF(bhc_ary_swig_ptr);
    Py_DECREF(bhc_view);
*/
    return ret;
}
