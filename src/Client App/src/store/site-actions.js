import { siteActions } from "./site-slice";
import { tokenConfig } from "../utils/tokenUtility";

import { notifyError, notifySuccess } from '../components/Toast/Toast';

import API from '../utils/api';


export const addSite = (item, callback = () => {}) => dispatch => {

    dispatch(siteActions.start_loading());

    let config = tokenConfig();

    API.post('api/site', item, config)
        .then(res => {
            dispatch(siteActions.add_site_success(res.data)); 
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
    API.put('api/site', item, config)
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
    API.patch(`api/site/${item.id}`, config)
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

    API.get(`api/site?pageNumber=${item.pageNumber}&pageSize=${item.pageSize}`, config)
        .then(res => {
            dispatch(siteActions.load_site_success(res.data));
        })
        .catch(err => {
            console.log(err.response);
            dispatch(siteActions.error());
        });
};