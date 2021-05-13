import { createSlice } from '@reduxjs/toolkit';

const authSlice = createSlice({
    name: 'auth',
    initialState: {
        token: localStorage.getItem('token'),
        isAuthenticated: false,
        user: JSON.parse(localStorage.getItem("user")),
        isLoading: false
    },
    reducers: {
        user_loading: (state) => {
            return {
                ...state,
                isLoading: true
            }
        },
        user_loaded: (state, action) => {
            return {
                ...state,
                isAuthenticated: true,
                isLoading: false,
                user: action.payload,
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
                isLoading: false
            }
        },
        login_fail: (state) => {

            localStorage.removeItem('token');
            localStorage.removeItem('user');

            return {
                ...state,
                token: null,
                user: null,
                isAuthenticated: false,
                isLoading: false
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
                isLoading: false
            }
        }

    }
});

export const authActions = authSlice.actions;

export default authSlice.reducer;