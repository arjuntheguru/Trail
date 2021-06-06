import { createSlice } from '@reduxjs/toolkit';
import {idle, loading, succeeded} from '../utils/stateStatus';

const authSlice = createSlice({
    name: 'auth',
    initialState: {
        token: localStorage.getItem('token'),
        isAuthenticated: false,
        user: JSON.parse(localStorage.getItem("user")),
        status: idle,
        error:''
    },
    reducers: {
        user_loading: (state) => {
            return {
                ...state,
                status : loading
            }
        },
        user_loaded: (state, action) => {
            return {
                ...state,
                isAuthenticated: true,
                status : succeeded,
                user: action.payload
            };
        },
        login_success: (state, action) => {

            localStorage.setItem('token', action.payload.token);           
            localStorage.setItem('user', JSON.stringify(action.payload));

            return {
                ...state,
                token: action.payload.token,
                user: action.payload,
                isAuthenticated: true,
                status: succeeded,
                error: ''
            }
        },
        login_fail: (state, action) => {

            localStorage.removeItem('token');
            localStorage.removeItem('user');

            return {
                ...state,
                token: null,
                user: null,
                isAuthenticated: false,
                status : idle,
                error: action.payload
            }
        },
        logout_success: (state) => {

            localStorage.removeItem('token');
            localStorage.removeItem('user');

            return {
                ...state,
                token: null,
                user: null,
                isAuthenticated: false,
                status: idle
            }
        }

    }
});

export const authActions = authSlice.actions;

export default authSlice.reducer;