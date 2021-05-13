import { authActions } from "./auth-slice";
import { tokenConfig } from "../utils/tokenUtility";
import { companyActions } from "./company-slice";
import { userActions } from "./user-slice";

import { notifyError, notifySuccess } from '../components/Toast/Toast';
import API from '../utils/api';

export const login = (userInfo, history) => dispatch => {

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
            if(res.status === 200) {                         
                history.push('/welcome');
                notifySuccess("Login successful");
            }
        })
        .catch((err) => {
            console.log(err);
            dispatch(authActions.login_fail());
            notifyError("Login failed");
        });

}

export const logout = () => (dispatch, getState) => {
    // headers
    let config = tokenConfig();
    
    const URL = "api/users/logout";
    API.post(URL, null, config)
        .then((res) => {
            dispatch(authActions.logout_success());
            dispatch(companyActions.reset());
            dispatch(userActions.reset());
        })
        .catch((err) => {
            console.log(err);
        });
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
        dispatch(authActions.login_fail());
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


    
