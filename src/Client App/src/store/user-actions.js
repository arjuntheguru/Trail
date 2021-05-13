import { tokenConfig } from "../utils/tokenUtility";

import { notifyError, notifySuccess } from '../components/Toast/Toast';
import { userActions } from "./user-slice";

import API from '../utils/api';

export const registerUser = (userInfo, callback = () => {}) => (dispatch) => {

    dispatch(userActions.start_loading());

    let config = tokenConfig();

    const body = JSON.stringify(userInfo);
    const URL = 'api/users/register';

    API.post(URL, body, config)
        .then((res) => {

            dispatch(userActions.register_user_success());
            notifySuccess("User created successfully");
            callback();
        })
        .catch((err) => {
            console.log(err);
            notifyError("User creation failed");
        });
};

export const getAdmin = (companyId) => (dispatch) => {

    dispatch(userActions.start_loading());

    let config = tokenConfig();

    const URL = `api/users/admin/${companyId}`;

    API.get(URL, config)
        .then((res) => {
           dispatch(userActions.load_user_success(res.data))
           notifySuccess("Admin loaded successfully");
        })
        .catch((err) => {
            console.log(err);
            notifyError("Failed to load admin");
        });
};

export const updateUser = (user) => (dispatch) => {

    dispatch(userActions.start_loading());

    let config = tokenConfig();

    const URL = `api/users/update`;

    API.put(URL, user, config)
        .then((res) => {
           dispatch(userActions.update_user_success(res.data))
           notifySuccess("User updated successfully");
        })
        .catch((err) => {   
            console.log(err);
            notifyError("Failed to update user");
        });
};

export const changePassword = (authenticateModal) => (dispatch) => {

    dispatch(userActions.start_loading());
    console.log(authenticateModal)
    let config = tokenConfig();

    const URL = `api/users/changePassword`;

    API.post(URL, authenticateModal, config)
    .then((res) => {
       dispatch(userActions.update_user_password_success(res.data))
       notifySuccess("Password updated successfully");
    })
    .catch((err) => {   
        console.log(err);
        notifyError("Failed to update password");
    });    

};

