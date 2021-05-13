import axios from "axios";
import { siteActions } from "./site-slice";
import { tokenConfig } from "../utils/tokenUtility";

import { notifyError, notifySuccess } from '../components/Toast/Toast';
import { registerUser } from "./user-actions";


export const addSite = (item, userInfo, callback) => dispatch => {

    dispatch(siteActions.start_loading());

    let config = tokenConfig();

    axios.post('https://localhost:44305/api/site', item, config)
        .then(res => {
            dispatch(siteActions.add_site_success(res.data));
            
            userInfo.siteId = res.data.data;
            dispatch(registerUser(userInfo));
            callback();
            notifySuccess(res.data.message);           
        })
        .catch(err => {
            console.log(err.response.data);
            dispatch(siteActions.error());
            notifyError("Error occured while adding Site");
        })
};

export const updateSite = (item) => dispatch => {

    dispatch(siteActions.start_loading());

    let config = tokenConfig();
    console.log(item);
    axios.put('https://localhost:44305/api/site', item, config)
        .then(res => {
            console.log(res);
            dispatch(siteActions.update_site_sucess(res.data));            
            notifySuccess("Site updated successfully");           
        })
        .catch(err => {
            console.log(err.response.data);
            dispatch(siteActions.error());
            notifyError("Error occured while adding Site");
        })
};

export const deleteSite = (item) => dispatch => {

    dispatch(siteActions.start_loading());

    let config = tokenConfig();
    console.log(item);
    axios.patch(`https://localhost:44305/api/site/${item.id}`, config)
        .then(res => {
            console.log(res);
            dispatch(siteActions.update_site_sucess(res.data));            
            notifySuccess("Site deleted successfully");           
        })
        .catch(err => {
            console.log(err.response.data);
            dispatch(siteActions.error());
            notifyError("Error occured while adding Site");
        })
};

export const getSite = (item = { pageNumber: 1, pageSize: 10 }) => (dispatch) => {

    dispatch(siteActions.start_loading());
    console.log(item);
    let config = tokenConfig();

    axios.get(`https://localhost:44305/api/site?pageNumber=${item.pageNumber}&pageSize=${item.pageSize}`, config)
        .then(res => {
            dispatch(siteActions.load_site_success(res.data));
        })
        .catch(err => {
            console.log(err.response);
            dispatch(siteActions.error());
        });
};