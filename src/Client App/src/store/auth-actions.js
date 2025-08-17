import { authActions } from "./auth-slice";
import { tokenConfig } from "../utils/tokenUtility";
import { companyActions } from "./company-slice";
import { userActions } from "./user-slice";
import { siteActions } from "./site-slice";
import { taskActions } from "./task-slice";
import { calendarActions } from "./calendar-slice";

import { notifyError, notifySuccess } from '../components/Toast/Toast';
import API from '../utils/api';


export const login = (userInfo, history) => dispatch => {
    dispatch(logout());
    dispatch(authActions.user_loading());

    const config = {
        headers: {
            "Content-Type": "application/json",
        },
    };

    const body = JSON.stringify(userInfo);
    const URL = 'api/users/login';

    API.post(URL, body, config)
        .then((res) => {
            dispatch(authActions.login_success(res.data));
            if (res.status === 200) {
                history.push('/welcome');
                notifySuccess("Login successful");
            }
        })
        .catch((err) => {
            console.log(err?.response);
            dispatch(authActions.login_fail(err?.response?.data?.message));
            notifyError(err?.response?.data?.message);
        });

}

export const logout = () => (dispatch) => {
   
    dispatch(authActions.logout_success());
    dispatch(companyActions.reset());
    dispatch(userActions.reset());
    dispatch(siteActions.reset());
    dispatch(taskActions.reset());
    dispatch(calendarActions.reset());    
};

export const loadUser = () => (dispatch, getState) => {
    // user loading
    dispatch(authActions.user_loading());
    // get token from state
    const token = getState().auth.token;
    // if token, add header to config
    if (token) {
        const userInfo = JSON.parse(localStorage.getItem("user"));
        dispatch(authActions.user_loaded(userInfo));
    } else {
        dispatch(authActions.login_fail("No user is logged in"));
    }
};

export const returnLoggedInUser = () => (dispatch, getState) => {
    // get token from state
    const token = getState().auth.token;
    // if token, add header to config
    if (token) {
        const userInfo = JSON.parse(localStorage.getItem("user"));
        return userInfo;
    } else {
        return null;
    }
};



